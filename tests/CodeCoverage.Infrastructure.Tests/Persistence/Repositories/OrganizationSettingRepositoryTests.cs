// ------------------------------------------------------------------------------------------------
// <copyright file="OrganizationSettingRepositoryTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.EntityFrameworkCore;

using CodeCoverage.Application.Exceptions;
using CodeCoverage.Application.Exceptions.ErrorCodes;
using CodeCoverage.Domain.Settings;
using CodeCoverage.Infrastructure.Identity;
using CodeCoverage.Infrastructure.Persistence;
using CodeCoverage.Infrastructure.Persistence.Repositories;
using CodeCoverage.Infrastructure.Tests.Support;

using static CodeCoverage.Infrastructure.Tests.Support.RepositorySeeder;

namespace CodeCoverage.Infrastructure.Tests.Persistence.Repositories;

// An organization table: every method against two organizations, on both engines.
[Collection(DatabaseCollection.Name)]
public sealed class OrganizationSettingRepositoryTests(DatabaseFixture database)
{
  #region Fields

  private static readonly SettingDefinition Prefix = SettingDefinitions.Find(SettingDefinitions.NotificationsStatusContextPrefix)!;

  #endregion

  #region Properties

  private static CancellationToken Ct => TestContext.Current.CancellationToken;

  #endregion

  #region Methods

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task Update_OtherOrganization_ShouldChangeNothing(DatabaseProvider provider)
  {
    var organizations = await TwoOrganizationDatabase.CreateAsync(database, provider, Ct);
    await SaveAsync(organizations, organizations.SecondId, "second");

    await SaveAsync(organizations, organizations.FirstId, "first");
    await SaveAsync(organizations, organizations.FirstId, "codecov");

    (await ListAsync(organizations, organizations.FirstId)).ShouldHaveSingleItem().Value.ShouldBe("codecov");
    var second = (await ListAsync(organizations, organizations.SecondId)).ShouldHaveSingleItem();
    second.Value.ShouldBe("second");
    second.Source.ShouldBe(SettingSource.Store);
    second.UpdatedBy.ShouldBe("octocat");
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task Remove_ShouldResetTheOrganizationOfTheScopeOnly(DatabaseProvider provider)
  {
    var organizations = await TwoOrganizationDatabase.CreateAsync(database, provider, Ct);
    await SaveAsync(organizations, organizations.FirstId, "first");
    await SaveAsync(organizations, organizations.SecondId, "second");

    await using (var context = organizations.ContextOf(organizations.FirstId))
    {
      var repository = new OrganizationSettingRepository(context);

      (await repository.RemoveAsync(Prefix, Ct)).ShouldBeTrue();
      (await repository.RemoveAsync(Prefix, Ct)).ShouldBeFalse();
    }

    (await ListAsync(organizations, organizations.FirstId)).ShouldBeEmpty();
    (await ListAsync(organizations, organizations.SecondId)).ShouldHaveSingleItem();
  }

  // Two first writes of the same key, forced: the other one saves between the read and the insert. The unique index
  // refuses this one, reported as a concurrent change, and nothing is left for a later save of the scope.
  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task Save_SameKeyInsertedMeanwhile_ShouldThrowConflictAndDetachTheRow(DatabaseProvider provider)
  {
    var organizations = await TwoOrganizationDatabase.CreateAsync(database, provider, Ct);
    var competing = new CompetingSaveChangesInterceptor(_ => SaveAsync(organizations, organizations.FirstId, "winner"));
    var organization = new OrganizationContext();
    organization.Enter(organizations.FirstId);
    var options = new DbContextOptionsBuilder<CodeCoverageDbContext>();
    options.UseCodeCoverageDatabase(provider, organizations.ConnectionString).AddInterceptors(competing);

    await using var context = new CodeCoverageDbContext(options.Options, organization);
    var repository = new OrganizationSettingRepository(context);

    var exception = await Should.ThrowAsync<ConflictException>(() => repository.SaveAsync(Prefix, "loser", "octocat", Now, Ct));

    exception.ErrorCode.ShouldBe(GeneralErrors.ConcurrentChange);
    exception.InnerException.ShouldBeOfType<DbUpdateException>();
    competing.Failed.ShouldBeTrue();
    context.ChangeTracker.Entries().ShouldBeEmpty("nothing is left for a later save of the scope");
    (await ListAsync(organizations, organizations.FirstId)).ShouldHaveSingleItem().Value.ShouldBe("winner");

    // The scope stays usable: the next save reads the row of the winner and updates it.
    await repository.SaveAsync(Prefix, "again", "octocat", Now, Ct);

    (await ListAsync(organizations, organizations.FirstId)).ShouldHaveSingleItem().Value.ShouldBe("again");
  }

  private static async Task SaveAsync(TwoOrganizationDatabase organizations, long organizationId, string value)
  {
    await using var context = organizations.ContextOf(organizationId);

    await new OrganizationSettingRepository(context).SaveAsync(Prefix, value, "octocat", Now, Ct);
  }

  private static async Task<IReadOnlyList<SettingValue>> ListAsync(TwoOrganizationDatabase organizations, long organizationId)
  {
    await using var context = organizations.ContextOf(organizationId);

    return await new OrganizationSettingRepository(context).ListAsync(Ct);
  }

  #endregion
}
