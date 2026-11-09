// ------------------------------------------------------------------------------------------------
// <copyright file="InstallationRepositoryTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.EntityFrameworkCore;

using CodeCoverage.Domain.Repositories;
using CodeCoverage.Infrastructure.Persistence;
using CodeCoverage.Infrastructure.Persistence.Repositories;
using CodeCoverage.Infrastructure.Tests.Support;

using static CodeCoverage.Infrastructure.Tests.Support.RepositorySeeder;

namespace CodeCoverage.Infrastructure.Tests.Persistence.Repositories;

// A global table: a webhook reads it to find its organization, before the scope has entered one.
[Collection(DatabaseCollection.Name)]
public sealed class InstallationRepositoryTests(DatabaseFixture database)
{
  #region Properties

  private static CancellationToken Ct => TestContext.Current.CancellationToken;

  #endregion

  #region Methods

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task Find_WithoutOrganization_ShouldReturnTheRow(DatabaseProvider provider)
  {
    var organizations = await TwoOrganizationDatabase.CreateAsync(database, provider, Ct);
    await AddInstallationsAsync(organizations, Ct);
    await using var context = organizations.ContextWithoutOrganization();

    var found = await new InstallationRepository(context).FindAsync(SecondInstallation, Ct);

    found.ShouldNotBeNull().OrganizationId.ShouldBe(organizations.SecondId);
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task Find_Unknown_ShouldReturnNull(DatabaseProvider provider)
  {
    var organizations = await TwoOrganizationDatabase.CreateAsync(database, provider, Ct);
    await using var context = organizations.ContextWithoutOrganization();

    (await new InstallationRepository(context).FindAsync(999, Ct)).ShouldBeNull();
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task Add_ShouldKeepTheIdentifierOfGitHub(DatabaseProvider provider)
  {
    var organizations = await TwoOrganizationDatabase.CreateAsync(database, provider, Ct);
    await using var context = organizations.ContextWithoutOrganization();
    var installation = Installation.Create(123456789012, organizations.FirstId, Now);

    await new InstallationRepository(context).AddAsync(installation, Ct);

    installation.Id.ShouldBe(123456789012);
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task Add_UnknownOrganization_ShouldThrow(DatabaseProvider provider)
  {
    var organizations = await TwoOrganizationDatabase.CreateAsync(database, provider, Ct);
    await using var context = organizations.ContextWithoutOrganization();

    await Should.ThrowAsync<DbUpdateException>(
      () => new InstallationRepository(context).AddAsync(Installation.Create(1, long.MaxValue, Now), Ct));
  }

  #endregion
}
