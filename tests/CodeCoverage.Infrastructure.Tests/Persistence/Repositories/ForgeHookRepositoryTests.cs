// ------------------------------------------------------------------------------------------------
// <copyright file="ForgeHookRepositoryTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.EntityFrameworkCore;

using CodeCoverage.Application.Exceptions;
using CodeCoverage.Application.Exceptions.ErrorCodes;
using CodeCoverage.Domain.Connections;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Infrastructure.Identity;
using CodeCoverage.Infrastructure.Persistence;
using CodeCoverage.Infrastructure.Persistence.Repositories;
using CodeCoverage.Infrastructure.Tests.Support;

namespace CodeCoverage.Infrastructure.Tests.Persistence.Repositories;

// An organization table: every method against two organizations, on both engines.
[Collection(DatabaseCollection.Name)]
public sealed class ForgeHookRepositoryTests(DatabaseFixture database)
{
  #region Fields

  private const string FirstConnection = "first-conn";

  private const string SecondConnection = "second-conn";

  private static readonly DateTimeOffset Now = new(2026, 11, 20, 8, 0, 0, TimeSpan.Zero);

  #endregion

  #region Properties

  private static CancellationToken Ct => TestContext.Current.CancellationToken;

  #endregion

  #region Methods

  [Fact]
  public async Task Methods_NullArgument_ShouldThrow()
  {
    await using var context = DatabaseFixture.CreateContext(Enum.GetValues<DatabaseProvider>()[0], "Server=localhost;Database=unused");
    var repository = new ForgeHookRepository(context);

    await Should.ThrowAsync<ArgumentNullException>(() => repository.ListAsync(null!, Ct));
    await Should.ThrowAsync<ArgumentNullException>(() => repository.UpsertAsync(null!, Ct));
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task ListAsync_OtherOrganization_ShouldReturnNothing(DatabaseProvider provider)
  {
    var organizations = await CreateAsync(provider);
    await UpsertAsync(organizations, organizations.FirstId, Hook(FirstConnection, ForgeHookScopeKind.Project, "project-a"));

    (await ListAsync(organizations, organizations.SecondId, FirstConnection)).ShouldBeEmpty();
    (await ListAsync(organizations, organizations.FirstId, FirstConnection)).ShouldHaveSingleItem().ScopeExternalId.ShouldBe("project-a");
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task ListAsync_SeveralScopes_ShouldReturnThoseOfTheConnectionInOrder(DatabaseProvider provider)
  {
    var organizations = await CreateAsync(provider);
    await UpsertAsync(organizations, organizations.FirstId, Hook(FirstConnection, ForgeHookScopeKind.Repository, "repo-1"));
    await UpsertAsync(organizations, organizations.FirstId, Hook(FirstConnection, ForgeHookScopeKind.Project, "project-b"));
    await UpsertAsync(organizations, organizations.FirstId, Hook(FirstConnection, ForgeHookScopeKind.Project, "project-a"));
    await UpsertAsync(organizations, organizations.FirstId, Hook(FirstConnection, ForgeHookScopeKind.Connection, "account"));

    var hooks = await ListAsync(organizations, organizations.FirstId, FirstConnection);

    hooks.Select(static h => (h.ScopeKind, h.ScopeExternalId))
         .ShouldBe(
           [
             (ForgeHookScopeKind.Connection, "account"),
             (ForgeHookScopeKind.Project, "project-a"),
             (ForgeHookScopeKind.Project, "project-b"),
             (ForgeHookScopeKind.Repository, "repo-1"),
           ]);

    foreach (var hook in hooks)
    {
      hook.Id.ShouldBeGreaterThan(0);
      hook.ConnectionId.ShouldBe(FirstConnection);
      hook.State.ShouldBe(ForgeHookState.Missing);
    }
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task UpsertAsync_SameScope_ShouldNotDuplicate(DatabaseProvider provider)
  {
    var organizations = await CreateAsync(provider);
    await UpsertAsync(organizations, organizations.FirstId, Hook(FirstConnection, ForgeHookScopeKind.Project, "project-a"));
    var checkedAgain = Hook(FirstConnection, ForgeHookScopeKind.Project, "PROJECT-A");
    checkedAgain.MarkDisabled("hook-1", "turned off", Now.AddDays(1));

    await UpsertAsync(organizations, organizations.FirstId, checkedAgain);

    var hook = (await ListAsync(organizations, organizations.FirstId, FirstConnection)).ShouldHaveSingleItem();
    hook.State.ShouldBe(ForgeHookState.Disabled);
    hook.ExternalId.ShouldBe("hook-1");
    hook.LastError.ShouldBe("turned off");
    hook.LastCheckedAt.ShouldBe(Now.AddDays(1));

    // A hook read back and checked again replaces the same row.
    hook.MarkActive("hook-2", Now.AddDays(2));
    await UpsertAsync(organizations, organizations.FirstId, hook);

    var active = (await ListAsync(organizations, organizations.FirstId, FirstConnection)).ShouldHaveSingleItem();
    active.Id.ShouldBe(hook.Id);
    active.State.ShouldBe(ForgeHookState.Active);
    active.ExternalId.ShouldBe("hook-2");
    active.LastError.ShouldBeNull();
    active.LastCheckedAt.ShouldBe(Now.AddDays(2));
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task UpsertAsync_OtherOrganization_ShouldChangeNothing(DatabaseProvider provider)
  {
    var organizations = await CreateAsync(provider);
    await UpsertAsync(organizations, organizations.SecondId, Hook(SecondConnection, ForgeHookScopeKind.Project, "project-a"));
    await UpsertAsync(organizations, organizations.FirstId, Hook(FirstConnection, ForgeHookScopeKind.Project, "project-a"));
    var forbidden = Hook(SecondConnection, ForgeHookScopeKind.Project, "project-a");
    forbidden.MarkForbidden("403", Now);

    // The connection of the second organization is out of reach of the first: the composite foreign key refuses the row,
    // and the hook of the second organization keeps its state.
    await Should.ThrowAsync<DbUpdateException>(() => UpsertAsync(organizations, organizations.FirstId, forbidden));

    (await ListAsync(organizations, organizations.SecondId, SecondConnection)).ShouldHaveSingleItem().State.ShouldBe(ForgeHookState.Missing);
    (await ListAsync(organizations, organizations.FirstId, FirstConnection)).ShouldHaveSingleItem().State.ShouldBe(ForgeHookState.Missing);
    (await ListAsync(organizations, organizations.FirstId, SecondConnection)).ShouldBeEmpty();
  }

  // Two first writes of the same scope, forced: the other one saves between the update that found nothing and the
  // insert. The unique index refuses this one, reported as a concurrent change, and nothing is left for a later save.
  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task UpsertAsync_SameScopeInsertedMeanwhile_ShouldThrowConflictAndDetachTheHook(DatabaseProvider provider)
  {
    var organizations = await CreateAsync(provider);
    var competing = new CompetingSaveChangesInterceptor(
      _ => UpsertAsync(organizations, organizations.FirstId, Hook(FirstConnection, ForgeHookScopeKind.Project, "project-a")));
    var organization = new OrganizationContext();
    organization.Enter(organizations.FirstId);
    var options = new DbContextOptionsBuilder<CodeCoverageDbContext>();
    options.UseCodeCoverageDatabase(provider, organizations.ConnectionString).AddInterceptors(competing);

    await using var context = new CodeCoverageDbContext(options.Options, organization);
    var repository = new ForgeHookRepository(context);
    var loser = Hook(FirstConnection, ForgeHookScopeKind.Project, "project-a");
    loser.MarkActive("hook-1", Now);

    var exception = await Should.ThrowAsync<ConflictException>(() => repository.UpsertAsync(loser, Ct));

    exception.ErrorCode.ShouldBe(GeneralErrors.ConcurrentChange);
    exception.InnerException.ShouldBeOfType<DbUpdateException>();
    competing.Failed.ShouldBeTrue();
    context.ChangeTracker.Entries().ShouldBeEmpty("nothing is left for a later save of the scope");

    // The scope stays usable: the next upsert finds the row of the winner and replaces its state.
    await repository.UpsertAsync(loser, Ct);

    var hook = (await ListAsync(organizations, organizations.FirstId, FirstConnection)).ShouldHaveSingleItem();
    hook.State.ShouldBe(ForgeHookState.Active);
    hook.ExternalId.ShouldBe("hook-1");
  }

  private static ForgeHook Hook(string connectionId, ForgeHookScopeKind scopeKind, string scopeExternalId) =>
    ForgeHook.Create(connectionId, scopeKind, scopeExternalId, Now);

  private async Task<TwoOrganizationDatabase> CreateAsync(DatabaseProvider provider)
  {
    var organizations = await TwoOrganizationDatabase.CreateAsync(database, provider, Ct);

    await using var context = organizations.ContextWithoutOrganization();
    context.ForgeConnections.Add(Connection(FirstConnection, organizations.FirstId));
    context.ForgeConnections.Add(Connection(SecondConnection, organizations.SecondId));
    await context.SaveChangesAsync(Ct);

    return organizations;
  }

  private static ForgeConnection Connection(string id, long organizationId) =>
    ForgeConnection.Create(
      id,
      organizationId,
      Forge.AzureDevOps,
      ForgeConnectionKind.AzureDevOpsEntraApplication,
      $"account-{id}",
      new Uri("https://dev.azure.com/contoso/"),
      Guid.NewGuid(),
      "service-account",
      "protected-credential",
      "protected-secret",
      null,
      Now);

  private static async Task UpsertAsync(TwoOrganizationDatabase organizations, long organizationId, ForgeHook hook)
  {
    await using var context = organizations.ContextOf(organizationId);

    await new ForgeHookRepository(context).UpsertAsync(hook, Ct);
  }

  private static async Task<IReadOnlyList<ForgeHook>> ListAsync(TwoOrganizationDatabase organizations, long organizationId, string connectionId)
  {
    await using var context = organizations.ContextOf(organizationId);

    return await new ForgeHookRepository(context).ListAsync(connectionId, Ct);
  }

  #endregion
}
