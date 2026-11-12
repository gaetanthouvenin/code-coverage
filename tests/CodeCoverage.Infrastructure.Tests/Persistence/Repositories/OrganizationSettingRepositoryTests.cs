// ------------------------------------------------------------------------------------------------
// <copyright file="OrganizationSettingRepositoryTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Settings;
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
