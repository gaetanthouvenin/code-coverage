// ------------------------------------------------------------------------------------------------
// <copyright file="UserRepositoryAccessRepositoryTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.EntityFrameworkCore;

using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Users;
using CodeCoverage.Infrastructure.Persistence;
using CodeCoverage.Infrastructure.Persistence.Repositories;
using CodeCoverage.Infrastructure.Tests.Support;

using static CodeCoverage.Infrastructure.Tests.Support.RepositorySeeder;

namespace CodeCoverage.Infrastructure.Tests.Persistence.Repositories;

// Every method against two organizations, on both engines: the rows of the other one are neither read nor written.
[Collection(DatabaseCollection.Name)]
public sealed class UserRepositoryAccessRepositoryTests(DatabaseFixture database)
{
  #region Properties

  private static CancellationToken Ct => TestContext.Current.CancellationToken;

  #endregion

  #region Methods

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task Upsert_New_ShouldStoreTheExternalIdentifierWithoutARepository(DatabaseProvider provider)
  {
    var (organizations, userId) = await SeedAsync(provider);

    await UpsertAsync(organizations, organizations.FirstId, userId, "NOT-TRACKED", RepositoryPermission.Write);

    await using var reader = organizations.ContextOf(organizations.FirstId);
    var access = (await new UserRepositoryAccessRepository(reader).FindAsync(userId, "not-tracked", Ct)).ShouldNotBeNull();

    access.RepositoryExternalId.ShouldBe("not-tracked");
    access.Permission.ShouldBe(RepositoryPermission.Write);
    access.RefreshedAt.ShouldBe(Now);
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task Upsert_Known_ShouldApplyThePermission(DatabaseProvider provider)
  {
    var (organizations, userId) = await SeedAsync(provider);
    await UpsertAsync(organizations, organizations.FirstId, userId, "1", RepositoryPermission.Write);

    await UpsertAsync(organizations, organizations.FirstId, userId, "1", RepositoryPermission.Admin, Now.AddHours(1));

    await using var reader = organizations.ContextOf(organizations.FirstId);
    var access = (await new UserRepositoryAccessRepository(reader).ListOfUserAsync(userId, Ct)).ShouldHaveSingleItem();

    access.Permission.ShouldBe(RepositoryPermission.Admin);
    access.RefreshedAt.ShouldBe(Now.AddHours(1));
  }

  // The same user and repository exist once per organization, and the rows do not see each other.
  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task Upsert_TwoOrganizations_ShouldKeepOneRowEach(DatabaseProvider provider)
  {
    var (organizations, userId) = await SeedAsync(provider);
    await UpsertAsync(organizations, organizations.FirstId, userId, "1", RepositoryPermission.Admin);
    await UpsertAsync(organizations, organizations.SecondId, userId, "1", RepositoryPermission.Read);

    await using var first = organizations.ContextOf(organizations.FirstId);
    await using var second = organizations.ContextOf(organizations.SecondId);

    (await new UserRepositoryAccessRepository(first).FindAsync(userId, "1", Ct)).ShouldNotBeNull().Permission.ShouldBe(RepositoryPermission.Admin);
    (await new UserRepositoryAccessRepository(second).FindAsync(userId, "1", Ct)).ShouldNotBeNull().Permission.ShouldBe(RepositoryPermission.Read);
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task Find_OtherOrganization_ShouldReturnNull(DatabaseProvider provider)
  {
    var (organizations, userId) = await SeedAsync(provider);
    await UpsertAsync(organizations, organizations.FirstId, userId, "1", RepositoryPermission.Admin);

    await using var other = organizations.ContextOf(organizations.SecondId);
    var repository = new UserRepositoryAccessRepository(other);

    (await repository.FindAsync(userId, "1", Ct)).ShouldBeNull();
    (await repository.ListOfUserAsync(userId, Ct)).ShouldBeEmpty();
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task Remove_OtherOrganization_ShouldLeaveTheRow(DatabaseProvider provider)
  {
    var (organizations, userId) = await SeedAsync(provider);
    await UpsertAsync(organizations, organizations.FirstId, userId, "1", RepositoryPermission.Admin);

    await using (var other = organizations.ContextOf(organizations.SecondId))
    {
      await new UserRepositoryAccessRepository(other).RemoveAsync(userId, "1", Ct);
    }

    await using var reader = organizations.ContextOf(organizations.FirstId);

    (await new UserRepositoryAccessRepository(reader).FindAsync(userId, "1", Ct)).ShouldNotBeNull();
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task Remove_ShouldDeleteTheRow(DatabaseProvider provider)
  {
    var (organizations, userId) = await SeedAsync(provider);
    await UpsertAsync(organizations, organizations.FirstId, userId, "ABC", RepositoryPermission.Admin);

    await using var context = organizations.ContextOf(organizations.FirstId);
    var repository = new UserRepositoryAccessRepository(context);

    await repository.RemoveAsync(userId, "abc", Ct);

    (await repository.FindAsync(userId, "abc", Ct)).ShouldBeNull();
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task Upsert_Concurrent_ShouldCreateOnce(DatabaseProvider provider)
  {
    var (organizations, userId) = await SeedAsync(provider);

    await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => UpsertAsync(organizations, organizations.FirstId, userId, "1", RepositoryPermission.Read)));

    await using var reader = organizations.ContextOf(organizations.FirstId);

    (await reader.UserRepositoryAccess.AsNoTracking().CountAsync(Ct)).ShouldBe(1);
  }

  private static async Task UpsertAsync(
    TwoOrganizationDatabase organizations,
    long organizationId,
    long userId,
    string externalId,
    RepositoryPermission permission,
    DateTimeOffset? at = null)
  {
    await using var context = organizations.ContextOf(organizationId);

    await new UserRepositoryAccessRepository(context).UpsertAsync(userId, externalId, permission, at ?? Now, Ct);
  }

  private async Task<(TwoOrganizationDatabase Organizations, long UserId)> SeedAsync(DatabaseProvider provider)
  {
    var organizations = await TwoOrganizationDatabase.CreateAsync(database, provider, Ct);
    await using var context = organizations.ContextWithoutOrganization();
    var user = AppUser.Create(Forge.GitHub, "abc", "octocat", null);

    await new UserRepository(context).TryAddAsync(user, Ct);

    return (organizations, user.Id);
  }

  #endregion
}
