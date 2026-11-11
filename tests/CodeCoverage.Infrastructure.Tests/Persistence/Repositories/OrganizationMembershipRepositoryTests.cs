// ------------------------------------------------------------------------------------------------
// <copyright file="OrganizationMembershipRepositoryTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.EntityFrameworkCore;

using CodeCoverage.Application.Exceptions;
using CodeCoverage.Application.Exceptions.ErrorCodes;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Users;
using CodeCoverage.Infrastructure.Identity;
using CodeCoverage.Infrastructure.Persistence;
using CodeCoverage.Infrastructure.Persistence.Repositories;
using CodeCoverage.Infrastructure.Tests.Support;

using static CodeCoverage.Infrastructure.Tests.Support.RepositorySeeder;

namespace CodeCoverage.Infrastructure.Tests.Persistence.Repositories;

// A global table: it links a user to several organizations, and is read before a scope enters one.
[Collection(DatabaseCollection.Name)]
public sealed class OrganizationMembershipRepositoryTests(DatabaseFixture database)
{
  #region Properties

  private static CancellationToken Ct => TestContext.Current.CancellationToken;

  #endregion

  #region Methods

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task Replace_ShouldAddUpdateAndRemoveInOneSave(DatabaseProvider provider)
  {
    var organizations = await TwoOrganizationDatabase.CreateAsync(database, provider, Ct);
    var user = await AddUserAsync(organizations, "1");

    await ReplaceAsync(organizations, user, Now, (organizations.FirstId, OrganizationRole.Admin));
    await ReplaceAsync(organizations, user, Now.AddMinutes(5), (organizations.FirstId, OrganizationRole.Participant), (organizations.SecondId, OrganizationRole.Admin));

    var both = await ListAsync(organizations, user);

    both.Select(static m => (m.OrganizationId, m.Role, m.RefreshedAt))
        .ShouldBe([(organizations.FirstId, OrganizationRole.Participant, Now.AddMinutes(5)), (organizations.SecondId, OrganizationRole.Admin, Now.AddMinutes(5))]);

    // The user left the first organization: its row goes, the other one is read again.
    await ReplaceAsync(organizations, user, Now.AddMinutes(10), (organizations.SecondId, OrganizationRole.Admin));

    var left = await ListAsync(organizations, user);

    left.ShouldHaveSingleItem().OrganizationId.ShouldBe(organizations.SecondId);
    left[0].RefreshedAt.ShouldBe(Now.AddMinutes(10));

    await ReplaceAsync(organizations, user, Now.AddMinutes(15));

    (await ListAsync(organizations, user)).ShouldBeEmpty();
  }

  // The same roles again: the date still moves, since it is what the hard expiry is counted from.
  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task Replace_SameRoles_ShouldStillMoveTheDate(DatabaseProvider provider)
  {
    var organizations = await TwoOrganizationDatabase.CreateAsync(database, provider, Ct);
    var user = await AddUserAsync(organizations, "1");

    await ReplaceAsync(organizations, user, Now, (organizations.FirstId, OrganizationRole.Admin));
    await ReplaceAsync(organizations, user, Now.AddMinutes(30), (organizations.FirstId, OrganizationRole.Admin));

    (await ListAsync(organizations, user)).ShouldHaveSingleItem().RefreshedAt.ShouldBe(Now.AddMinutes(30));
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task Replace_ShouldNeverTouchTheRowsOfAnotherUser(DatabaseProvider provider)
  {
    var organizations = await TwoOrganizationDatabase.CreateAsync(database, provider, Ct);
    var user = await AddUserAsync(organizations, "1");
    var other = await AddUserAsync(organizations, "2");

    await ReplaceAsync(organizations, other, Now, (organizations.FirstId, OrganizationRole.Admin), (organizations.SecondId, OrganizationRole.Participant));
    await ReplaceAsync(organizations, user, Now.AddMinutes(5), (organizations.FirstId, OrganizationRole.Participant));
    await ReplaceAsync(organizations, user, Now.AddMinutes(10));

    (await ListAsync(organizations, user)).ShouldBeEmpty();
    (await ListAsync(organizations, other)).Select(static m => (m.OrganizationId, m.Role, m.RefreshedAt))
                                           .ShouldBe([(organizations.FirstId, OrganizationRole.Admin, Now), (organizations.SecondId, OrganizationRole.Participant, Now)]);
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task Find_ShouldReadTheLinkOfThatUserAndThatOrganizationOnly(DatabaseProvider provider)
  {
    var organizations = await TwoOrganizationDatabase.CreateAsync(database, provider, Ct);
    var user = await AddUserAsync(organizations, "1");
    var other = await AddUserAsync(organizations, "2");

    await ReplaceAsync(organizations, user, Now, (organizations.FirstId, OrganizationRole.Admin));
    await ReplaceAsync(organizations, other, Now, (organizations.SecondId, OrganizationRole.Participant));

    // Read from a scope that entered the other organization: the table is global, the arguments alone decide.
    await using var context = organizations.ContextOf(organizations.SecondId);
    var repository = new OrganizationMembershipRepository(context);

    (await repository.FindAsync(user, organizations.FirstId, Ct)).ShouldNotBeNull().Role.ShouldBe(OrganizationRole.Admin);
    (await repository.FindAsync(user, organizations.SecondId, Ct)).ShouldBeNull();
    (await repository.FindAsync(other, organizations.FirstId, Ct)).ShouldBeNull();
    (await repository.FindAsync(other, organizations.SecondId, Ct)).ShouldNotBeNull().Role.ShouldBe(OrganizationRole.Participant);
    context.ChangeTracker.Entries().ShouldBeEmpty();
  }

  // Two refreshes of the same user that overlap, forced: both find no row and insert the same link. The unique
  // index refuses the second one, which is reported as a conflict, and the job runs again.
  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task Replace_TwoAtTheSameTime_ShouldLeaveOneRowAndReportAConflict(DatabaseProvider provider)
  {
    var organizations = await TwoOrganizationDatabase.CreateAsync(database, provider, Ct);

    for (var round = 0; round < 10; round++)
    {
      var user = await AddUserAsync(organizations, "race-" + round.ToString(System.Globalization.CultureInfo.InvariantCulture));
      var holding = new HoldingCommandInterceptor(static sql => sql.Contains("INSERT", StringComparison.OrdinalIgnoreCase)
                                                               && sql.Contains("OrganizationMemberships", StringComparison.Ordinal));

      var holder = ReplaceInItsOwnContextAsync(organizations, provider, holding, user, OrganizationRole.Participant, Now);
      await holding.Reached.Task.WaitAsync(TimeSpan.FromMinutes(1), Ct);

      await ReplaceInItsOwnContextAsync(organizations, provider, null, user, OrganizationRole.Admin, Now.AddMinutes(1));

      holding.Gate.SetResult();

      var exception = await Should.ThrowAsync<ConflictException>(() => holder.WaitAsync(TimeSpan.FromMinutes(1), Ct));

      exception.ErrorCode.ShouldBe(GeneralErrors.ConcurrentChange);

      var kept = (await ListAsync(organizations, user)).ShouldHaveSingleItem();

      kept.Role.ShouldBe(OrganizationRole.Admin);
      kept.RefreshedAt.ShouldBe(Now.AddMinutes(1));
    }
  }

  // The other way round: the held refresh updates a row the other one removed meanwhile.
  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task Replace_RowRemovedMeanwhile_ShouldReportAConflictAndLeaveTheScopeUsable(DatabaseProvider provider)
  {
    var organizations = await TwoOrganizationDatabase.CreateAsync(database, provider, Ct);
    var user = await AddUserAsync(organizations, "1");
    await ReplaceAsync(organizations, user, Now, (organizations.FirstId, OrganizationRole.Admin));

    var holding = new HoldingCommandInterceptor(static sql => sql.Contains("UPDATE", StringComparison.OrdinalIgnoreCase)
                                                             && sql.Contains("OrganizationMemberships", StringComparison.Ordinal));
    var options = new DbContextOptionsBuilder<CodeCoverageDbContext>();
    options.UseCodeCoverageDatabase(provider, organizations.ConnectionString).AddInterceptors(holding);

    await using var context = new CodeCoverageDbContext(options.Options, new OrganizationContext());
    var repository = new OrganizationMembershipRepository(context);

    var holder = repository.ReplaceAsync(user, Roles((organizations.FirstId, OrganizationRole.Participant)), Now.AddMinutes(5), Ct);
    await holding.Reached.Task.WaitAsync(TimeSpan.FromMinutes(1), Ct);

    await ReplaceAsync(organizations, user, Now.AddMinutes(1));

    holding.Gate.SetResult();

    (await Should.ThrowAsync<ConflictException>(() => holder.WaitAsync(TimeSpan.FromMinutes(1), Ct))).ErrorCode.ShouldBe(GeneralErrors.ConcurrentChange);
    (await ListAsync(organizations, user)).ShouldBeEmpty();

    // Nothing of the lost refresh is left in the scope: the next one of the same scope is saved.
    context.ChangeTracker.Entries().ShouldBeEmpty();
    await repository.ReplaceAsync(user, Roles((organizations.SecondId, OrganizationRole.Admin)), Now.AddMinutes(10), Ct);

    (await ListAsync(organizations, user)).ShouldHaveSingleItem().OrganizationId.ShouldBe(organizations.SecondId);
  }

  // A membership never outlives its user or its organization silently: the database refuses to remove them.
  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task Replace_UnknownUserOrOrganization_ShouldBeRefusedByTheDatabase(DatabaseProvider provider)
  {
    var organizations = await TwoOrganizationDatabase.CreateAsync(database, provider, Ct);
    var user = await AddUserAsync(organizations, "1");

    await Should.ThrowAsync<DbUpdateException>(() => ReplaceAsync(organizations, user, Now, (organizations.FirstId + 100_000, OrganizationRole.Admin)));
    await Should.ThrowAsync<DbUpdateException>(() => ReplaceAsync(organizations, user + 100_000, Now, (organizations.FirstId, OrganizationRole.Admin)));

    (await ListAsync(organizations, user)).ShouldBeEmpty();
  }

  [Fact]
  public async Task Replace_Null_ShouldThrow()
  {
    await using var context = DatabaseFixture.CreateContext(Enum.GetValues<DatabaseProvider>()[0], "Server=localhost;Database=unused");

    await Should.ThrowAsync<ArgumentNullException>(() => new OrganizationMembershipRepository(context).ReplaceAsync(1, null!, Now, Ct));
  }

  private static Dictionary<long, OrganizationRole> Roles(params (long OrganizationId, OrganizationRole Role)[] roles) =>
    roles.ToDictionary(static r => r.OrganizationId, static r => r.Role);

  private static async Task<long> AddUserAsync(TwoOrganizationDatabase organizations, string externalId)
  {
    await using var context = organizations.ContextWithoutOrganization();
    var user = AppUser.Create(Forge.GitHub, externalId, "user-" + externalId, null);

    (await new UserRepository(context).TryAddAsync(user, Ct)).ShouldBeTrue();

    return user.Id;
  }

  private static async Task ReplaceAsync(
    TwoOrganizationDatabase organizations,
    long userId,
    DateTimeOffset at,
    params (long OrganizationId, OrganizationRole Role)[] roles)
  {
    await using var context = organizations.ContextWithoutOrganization();

    await new OrganizationMembershipRepository(context).ReplaceAsync(userId, Roles(roles), at, Ct);
  }

  private static async Task<IReadOnlyList<OrganizationMembership>> ListAsync(TwoOrganizationDatabase organizations, long userId)
  {
    await using var context = organizations.ContextWithoutOrganization();

    return await new OrganizationMembershipRepository(context).ListOfUserAsync(userId, Ct);
  }

  private static async Task ReplaceInItsOwnContextAsync(
    TwoOrganizationDatabase organizations,
    DatabaseProvider provider,
    HoldingCommandInterceptor? interceptor,
    long userId,
    OrganizationRole role,
    DateTimeOffset at)
  {
    var options = new DbContextOptionsBuilder<CodeCoverageDbContext>();
    options.UseCodeCoverageDatabase(provider, organizations.ConnectionString);

    if (interceptor is not null)
    {
      options.AddInterceptors(interceptor);
    }

    await using var context = new CodeCoverageDbContext(options.Options, new OrganizationContext());

    await new OrganizationMembershipRepository(context).ReplaceAsync(userId, Roles((organizations.FirstId, role)), at, Ct);
  }

  #endregion
}
