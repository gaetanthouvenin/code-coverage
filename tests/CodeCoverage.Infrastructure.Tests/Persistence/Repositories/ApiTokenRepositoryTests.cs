// ------------------------------------------------------------------------------------------------
// <copyright file="ApiTokenRepositoryTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Security.Cryptography;
using System.Text;

using Microsoft.EntityFrameworkCore;

using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Tokens;
using CodeCoverage.Domain.Users;
using CodeCoverage.Infrastructure.Persistence;
using CodeCoverage.Infrastructure.Persistence.Repositories;
using CodeCoverage.Infrastructure.Tests.Support;

using static CodeCoverage.Infrastructure.Tests.Support.RepositorySeeder;

namespace CodeCoverage.Infrastructure.Tests.Persistence.Repositories;

// A global table: a token is found by its hash before any organization is entered.
[Collection(DatabaseCollection.Name)]
public sealed class ApiTokenRepositoryTests(DatabaseFixture database)
{
  #region Properties

  private static CancellationToken Ct => TestContext.Current.CancellationToken;

  #endregion

  #region Methods

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task FindByHash_FromAnyScope_ShouldReadTheTokenOfEitherOrganization(DatabaseProvider provider)
  {
    var organizations = await TwoOrganizationDatabase.CreateAsync(database, provider, Ct);
    var user = await AddUserAsync(organizations, "1");
    var first = await AddTokenAsync(organizations, user, organizations.FirstId, "cvp_first");
    var instance = await AddTokenAsync(organizations, user, null, "cvp_instance");

    // Read from a scope that entered the other organization: the table is global, the hash alone decides.
    await using var context = organizations.ContextOf(organizations.SecondId);
    var repository = new ApiTokenRepository(context);

    var found = (await repository.FindByHashAsync(Hash("cvp_first"), Ct)).ShouldNotBeNull();
    found.Id.ShouldBe(first);
    found.OrganizationId.ShouldBe(organizations.FirstId);
    (await repository.FindByHashAsync(Hash("cvp_instance"), Ct)).ShouldNotBeNull().Id.ShouldBe(instance);
    (await repository.FindByHashAsync(Hash("cvp_unknown"), Ct)).ShouldBeNull();
    context.ChangeTracker.Entries().ShouldBeEmpty();
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task FindOfUser_ShouldNotReadTheTokenOfAnotherUser(DatabaseProvider provider)
  {
    var organizations = await TwoOrganizationDatabase.CreateAsync(database, provider, Ct);
    var user = await AddUserAsync(organizations, "1");
    var other = await AddUserAsync(organizations, "2");
    var token = await AddTokenAsync(organizations, user, organizations.FirstId, "cvp_mine");

    await using var context = organizations.ContextWithoutOrganization();
    var repository = new ApiTokenRepository(context);

    (await repository.FindOfUserAsync(other, token, Ct)).ShouldBeNull();
    (await repository.FindOfUserAsync(user, token, Ct)).ShouldNotBeNull();
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task Revoke_ShouldBeSavedAndListedMostRecentFirst(DatabaseProvider provider)
  {
    var organizations = await TwoOrganizationDatabase.CreateAsync(database, provider, Ct);
    var user = await AddUserAsync(organizations, "1");
    var older = await AddTokenAsync(organizations, user, organizations.FirstId, "cvp_older");
    var newer = await AddTokenAsync(organizations, user, null, "cvp_newer");

    await using (var context = organizations.ContextWithoutOrganization())
    {
      var repository = new ApiTokenRepository(context);
      var token = (await repository.FindOfUserAsync(user, older, Ct)).ShouldNotBeNull();

      token.Revoke(Now.AddMinutes(1));
      await repository.UpdateAsync(token, Ct);
    }

    await using var reading = organizations.ContextWithoutOrganization();
    var listed = await new ApiTokenRepository(reading).ListOfUserAsync(user, Ct);

    listed.Select(static t => t.Id).ShouldBe([newer, older]);
    listed[1].RevokedAt.ShouldBe(Now.AddMinutes(1));
    listed[0].RevokedAt.ShouldBeNull();
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task RecordUse_ShouldWriteAtMostOnceAMinute(DatabaseProvider provider)
  {
    var organizations = await TwoOrganizationDatabase.CreateAsync(database, provider, Ct);
    var user = await AddUserAsync(organizations, "1");
    var token = await AddTokenAsync(organizations, user, organizations.FirstId, "cvp_used");

    await RecordUseAsync(organizations, token, Now);
    await RecordUseAsync(organizations, token, Now.AddSeconds(30));
    (await FindAsync(organizations, user, token)).LastUsedAt.ShouldBe(Now);

    await RecordUseAsync(organizations, token, Now.AddMinutes(1));
    (await FindAsync(organizations, user, token)).LastUsedAt.ShouldBe(Now.AddMinutes(1));
  }

  // A token never outlives its user or its organization silently, and two tokens never share a value.
  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task Add_UnknownUserOrDuplicateHash_ShouldBeRefusedByTheDatabase(DatabaseProvider provider)
  {
    var organizations = await TwoOrganizationDatabase.CreateAsync(database, provider, Ct);
    var user = await AddUserAsync(organizations, "1");
    await AddTokenAsync(organizations, user, organizations.FirstId, "cvp_taken");

    await Should.ThrowAsync<DbUpdateException>(() => AddTokenAsync(organizations, user + 100_000, null, "cvp_orphan"));
    await Should.ThrowAsync<DbUpdateException>(() => AddTokenAsync(organizations, user, organizations.FirstId + 100_000, "cvp_lost"));
    await Should.ThrowAsync<DbUpdateException>(() => AddTokenAsync(organizations, user, organizations.SecondId, "cvp_taken"));
  }

  private static byte[] Hash(string value) => SHA256.HashData(Encoding.UTF8.GetBytes(value));

  private static async Task<long> AddUserAsync(TwoOrganizationDatabase organizations, string externalId)
  {
    await using var context = organizations.ContextWithoutOrganization();
    var user = AppUser.Create(Forge.GitHub, externalId, "user-" + externalId, null);

    (await new UserRepository(context).TryAddAsync(user, Ct)).ShouldBeTrue();

    return user.Id;
  }

  private static async Task<long> AddTokenAsync(TwoOrganizationDatabase organizations, long userId, long? organizationId, string value)
  {
    await using var context = organizations.ContextWithoutOrganization();
    var token = ApiToken.Create(userId, organizationId, "token", value[..8], Hash(value), Now, null);

    await new ApiTokenRepository(context).AddAsync(token, Ct);

    return token.Id;
  }

  private static async Task RecordUseAsync(TwoOrganizationDatabase organizations, long tokenId, DateTimeOffset at)
  {
    await using var context = organizations.ContextWithoutOrganization();

    await new ApiTokenRepository(context).RecordUseAsync(tokenId, at, Ct);
  }

  private static async Task<ApiToken> FindAsync(TwoOrganizationDatabase organizations, long userId, long tokenId)
  {
    await using var context = organizations.ContextWithoutOrganization();

    return (await new ApiTokenRepository(context).FindOfUserAsync(userId, tokenId, Ct)).ShouldNotBeNull();
  }

  #endregion
}
