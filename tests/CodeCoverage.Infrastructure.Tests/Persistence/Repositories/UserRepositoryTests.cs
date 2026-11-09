// ------------------------------------------------------------------------------------------------
// <copyright file="UserRepositoryTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.EntityFrameworkCore;

using CodeCoverage.Application.Exceptions;
using CodeCoverage.Application.Exceptions.ErrorCodes;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Domain.Users;
using CodeCoverage.Infrastructure.Persistence;
using CodeCoverage.Infrastructure.Persistence.Repositories;
using CodeCoverage.Infrastructure.Tests.Support;

using static CodeCoverage.Infrastructure.Tests.Support.RepositorySeeder;

namespace CodeCoverage.Infrastructure.Tests.Persistence.Repositories;

// A global table: read before a scope enters an organization.
[Collection(DatabaseCollection.Name)]
public sealed class UserRepositoryTests(DatabaseFixture database)
{
  #region Properties

  private static CancellationToken Ct => TestContext.Current.CancellationToken;

  #endregion

  #region Methods

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task TryAdd_ShouldBeFoundWithoutOrganization(DatabaseProvider provider)
  {
    var organizations = await TwoOrganizationDatabase.CreateAsync(database, provider, Ct);
    var user = await AddAsync(organizations);

    await using var reader = organizations.ContextWithoutOrganization();
    var repository = new UserRepository(reader);

    (await repository.FindAsync(user.Id, Ct)).ShouldNotBeNull().Login.ShouldBe("octocat");
    (await repository.FindByExternalIdAsync(Forge.GitHub, "ABC", Ct)).ShouldNotBeNull().Id.ShouldBe(user.Id);
    (await repository.FindByExternalIdAsync(Forge.GitLab, "abc", Ct)).ShouldBeNull();
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task TryAdd_SameAccount_ShouldReturnFalseAndLeaveTheScopeUsable(DatabaseProvider provider)
  {
    var organizations = await TwoOrganizationDatabase.CreateAsync(database, provider, Ct);
    await AddAsync(organizations);

    await using var context = organizations.ContextWithoutOrganization();
    var repository = new UserRepository(context);

    (await repository.TryAddAsync(AppUser.Create(Forge.GitHub, "abc", "other", null), Ct)).ShouldBeFalse();
    (await repository.TryAddAsync(AppUser.Create(Forge.GitHub, "def", "another", null), Ct)).ShouldBeTrue();
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task Update_ShouldStoreOnlyProtectedTokens(DatabaseProvider provider)
  {
    var organizations = await TwoOrganizationDatabase.CreateAsync(database, provider, Ct);
    var user = await AddAsync(organizations);
    var protector = A.Fake<ISecretProtector>();
    A.CallTo(() => protector.Protect("ghu_clear")).Returns("sealed-access");

    await using (var writer = organizations.ContextWithoutOrganization())
    {
      var repository = new UserRepository(writer);
      var read = (await repository.FindAsync(user.Id, Ct)).ShouldNotBeNull();
      read.ReplaceTokens(protector, "ghu_clear", Now.AddHours(8), null, null);
      await repository.UpdateAsync(read, Ct);
    }

    await using var reader = organizations.ContextWithoutOrganization();
    var saved = await reader.Users.AsNoTracking().SingleAsync(Ct);

    saved.AccessTokenProtected.ShouldBe("sealed-access");
    saved.AccessTokenExpiresAt.ShouldBe(Now.AddHours(8));
    saved.Version.ShouldBe(1);
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task Update_StaleVersion_ShouldThrowConflict(DatabaseProvider provider)
  {
    var organizations = await TwoOrganizationDatabase.CreateAsync(database, provider, Ct);
    var user = await AddAsync(organizations);

    await using var stale = organizations.ContextWithoutOrganization();
    var repository = new UserRepository(stale);
    var loser = (await repository.FindAsync(user.Id, Ct)).ShouldNotBeNull();

    await using (var winner = organizations.ContextWithoutOrganization())
    {
      var other = new UserRepository(winner);
      var read = (await other.FindAsync(user.Id, Ct)).ShouldNotBeNull();
      read.RecordSignIn("first", null, Now);
      await other.UpdateAsync(read, Ct);
    }

    loser.RecordSignIn("second", null, Now);

    var exception = await Should.ThrowAsync<ConflictException>(() => repository.UpdateAsync(loser, Ct));

    exception.ErrorCode.ShouldBe(GeneralErrors.ConcurrentChange);
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task Update_NotRead_ShouldThrow(DatabaseProvider provider)
  {
    var organizations = await TwoOrganizationDatabase.CreateAsync(database, provider, Ct);
    await using var context = organizations.ContextWithoutOrganization();

    await Should.ThrowAsync<InvalidOperationException>(() => new UserRepository(context).UpdateAsync(AppUser.Create(Forge.GitHub, "1", "octocat", null), Ct));
  }

  private static async Task<AppUser> AddAsync(TwoOrganizationDatabase organizations)
  {
    await using var context = organizations.ContextWithoutOrganization();
    var user = AppUser.Create(Forge.GitHub, "abc", "octocat", null);

    (await new UserRepository(context).TryAddAsync(user, Ct)).ShouldBeTrue();

    return user;
  }

  #endregion
}
