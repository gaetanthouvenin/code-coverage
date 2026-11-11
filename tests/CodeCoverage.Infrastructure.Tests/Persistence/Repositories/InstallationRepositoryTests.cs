// ------------------------------------------------------------------------------------------------
// <copyright file="InstallationRepositoryTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.EntityFrameworkCore;

using CodeCoverage.Domain.Organizations;
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
  public async Task FindOwner_ShouldGiveTheOrganizationAndItsState(DatabaseProvider provider)
  {
    var organizations = await TwoOrganizationDatabase.CreateAsync(database, provider, Ct);
    await AddInstallationsAsync(organizations, Ct);

    await using (var writer = organizations.ContextWithoutOrganization())
    {
      var first = await writer.Organizations.SingleAsync(o => o.Id == organizations.FirstId, Ct);
      first.Admit("admin", Now);
      await writer.SaveChangesAsync(Ct);
    }

    await using var context = organizations.ContextWithoutOrganization();
    var repository = new InstallationRepository(context);

    (await repository.FindOwnerAsync(FirstInstallation, Ct)).ShouldBe(new InstallationOwner(organizations.FirstId, OrganizationState.Active));
    (await repository.FindOwnerAsync(SecondInstallation, Ct)).ShouldBe(new InstallationOwner(organizations.SecondId, OrganizationState.Pending));
  }

  // F20, F21, S7. The installations of an organization, the most recent first: GitHub gives its identifiers in
  // increasing order, so the highest one is the current one, whatever the order they were recorded in here (a late
  // or redelivered creation is recorded after a more recent installation).
  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task ListIdsForOrganization_ShouldGiveItsInstallationsTheHighestIdentifierFirst(DatabaseProvider provider)
  {
    var organizations = await TwoOrganizationDatabase.CreateAsync(database, provider, Ct);

    await using (var context = organizations.ContextWithoutOrganization())
    {
      var repository = new InstallationRepository(context);

      await repository.AddAsync(Installation.Create(30, organizations.FirstId, Now.AddDays(-2)), Ct);
      await repository.AddAsync(Installation.Create(10, organizations.FirstId, Now), Ct);
      await repository.AddAsync(Installation.Create(20, organizations.FirstId, Now.AddDays(-1)), Ct);
      await repository.AddAsync(Installation.Create(40, organizations.SecondId, Now), Ct);
    }

    await using var reader = organizations.ContextWithoutOrganization();
    var installations = new InstallationRepository(reader);

    (await installations.ListIdsForOrganizationAsync(organizations.FirstId, Ct)).ShouldBe([30L, 20L, 10L]);
    (await installations.ListIdsForOrganizationAsync(organizations.SecondId, Ct)).ShouldBe([40L]);
    (await installations.ListIdsForOrganizationAsync(long.MaxValue, Ct)).ShouldBeEmpty();
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task FindOwner_Unknown_ShouldReturnNull(DatabaseProvider provider)
  {
    var organizations = await TwoOrganizationDatabase.CreateAsync(database, provider, Ct);
    await using var context = organizations.ContextWithoutOrganization();

    (await new InstallationRepository(context).FindOwnerAsync(999, Ct)).ShouldBeNull();
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
