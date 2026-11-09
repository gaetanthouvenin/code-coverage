// ------------------------------------------------------------------------------------------------
// <copyright file="UploadTokenRepositoryTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Tokens;
using CodeCoverage.Infrastructure.Persistence;
using CodeCoverage.Infrastructure.Persistence.Repositories;
using CodeCoverage.Infrastructure.Tests.Support;

using static CodeCoverage.Infrastructure.Tests.Support.RepositorySeeder;

namespace CodeCoverage.Infrastructure.Tests.Persistence.Repositories;

// Every method against two organizations, on both engines: the rows of the other one are neither read nor written.
[Collection(DatabaseCollection.Name)]
public sealed class UploadTokenRepositoryTests(DatabaseFixture database)
{
  #region Fields

  private static readonly byte[] Hash = Enumerable.Repeat((byte)7, UploadToken.HashLength).ToArray();

  private static readonly byte[] OtherHash = Enumerable.Repeat((byte)8, UploadToken.HashLength).ToArray();

  #endregion

  #region Properties

  private static CancellationToken Ct => TestContext.Current.CancellationToken;

  #endregion

  #region Methods

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task TryAdd_ShouldStoreTheToken(DatabaseProvider provider)
  {
    var seed = await AddRepositoriesAsync(database, provider, Ct);
    var token = OrganizationToken(Hash);

    await using (var writer = seed.Organizations.ContextOf(seed.Organizations.FirstId))
    {
      (await new UploadTokenRepository(writer).TryAddAsync(token, Ct)).ShouldBeTrue();
    }

    await using var reader = seed.Organizations.ContextOf(seed.Organizations.FirstId);
    var saved = (await new UploadTokenRepository(reader).FindAsync(token.Id, Ct)).ShouldNotBeNull();

    saved.Hash.ShouldBe(Hash);
    saved.Scope.ToString().ShouldBe(nameof(UploadTokenScope.Organization));
    saved.Label.ShouldBe("ci");
    saved.Hint.ShouldBe("hint");
    saved.CreatedBy.ShouldBe("octocat");
    saved.CreatedAt.ShouldBe(Now);
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task TryAdd_SameValueInOtherOrganization_ShouldStoreBoth(DatabaseProvider provider)
  {
    var seed = await AddRepositoriesAsync(database, provider, Ct);

    await using var first = seed.Organizations.ContextOf(seed.Organizations.FirstId);
    await using var second = seed.Organizations.ContextOf(seed.Organizations.SecondId);

    (await new UploadTokenRepository(first).TryAddAsync(OrganizationToken(Hash), Ct)).ShouldBeTrue();
    (await new UploadTokenRepository(second).TryAddAsync(OrganizationToken(Hash), Ct)).ShouldBeTrue();
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task TryAdd_SameValueTwiceInOrganization_ShouldReturnFalseAndLeaveTheScopeUsable(DatabaseProvider provider)
  {
    var seed = await AddRepositoriesAsync(database, provider, Ct);
    await using var context = seed.Organizations.ContextOf(seed.Organizations.FirstId);
    var tokens = new UploadTokenRepository(context);
    await tokens.TryAddAsync(OrganizationToken(Hash), Ct);

    (await tokens.TryAddAsync(OrganizationToken(Hash), Ct)).ShouldBeFalse();

    (await tokens.TryAddAsync(OrganizationToken(OtherHash), Ct)).ShouldBeTrue();
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task Find_OtherOrganization_ShouldReturnNull(DatabaseProvider provider)
  {
    var seed = await AddRepositoriesAsync(database, provider, Ct);
    var token = OrganizationToken(Hash);

    await using (var writer = seed.Organizations.ContextOf(seed.Organizations.FirstId))
    {
      await new UploadTokenRepository(writer).TryAddAsync(token, Ct);
    }

    await using var other = seed.Organizations.ContextOf(seed.Organizations.SecondId);

    (await new UploadTokenRepository(other).FindAsync(token.Id, Ct)).ShouldBeNull();
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task List_ShouldSeparateTheScopesAndTheOrganizations(DatabaseProvider provider)
  {
    var seed = await AddRepositoriesAsync(database, provider, Ct);

    await using var first = seed.Organizations.ContextOf(seed.Organizations.FirstId);
    await using var second = seed.Organizations.ContextOf(seed.Organizations.SecondId);
    var tokens = new UploadTokenRepository(first);
    var others = new UploadTokenRepository(second);
    var organizationToken = OrganizationToken(Hash);
    var repositoryToken = UploadToken.Create(UploadTokenScope.Repository, seed.FirstRepository, "repo", "hint", OtherHash, false, "octocat", Now, null);
    await tokens.TryAddAsync(organizationToken, Ct);
    await tokens.TryAddAsync(repositoryToken, Ct);
    await others.TryAddAsync(OrganizationToken(Enumerable.Repeat((byte)9, UploadToken.HashLength).ToArray()), Ct);

    (await tokens.ListOfOrganizationAsync(Ct)).ShouldHaveSingleItem().Id.ShouldBe(organizationToken.Id);
    (await tokens.ListOfRepositoryAsync(seed.FirstRepository, Ct)).ShouldHaveSingleItem().Id.ShouldBe(repositoryToken.Id);
    (await others.ListOfRepositoryAsync(seed.FirstRepository, Ct)).ShouldBeEmpty();
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task Update_ShouldSaveTheRevocation(DatabaseProvider provider)
  {
    var seed = await AddRepositoriesAsync(database, provider, Ct);
    var added = OrganizationToken(Hash);

    await using (var writer = seed.Organizations.ContextOf(seed.Organizations.FirstId))
    {
      await new UploadTokenRepository(writer).TryAddAsync(added, Ct);
    }

    await using (var context = seed.Organizations.ContextOf(seed.Organizations.FirstId))
    {
      var tokens = new UploadTokenRepository(context);
      var token = (await tokens.FindAsync(added.Id, Ct)).ShouldNotBeNull();
      token.Revoke(Now);
      await tokens.UpdateAsync(token, Ct);
    }

    await using var reader = seed.Organizations.ContextOf(seed.Organizations.FirstId);

    (await new UploadTokenRepository(reader).FindAsync(added.Id, Ct)).ShouldNotBeNull().RevokedAt.ShouldBe(Now);
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task Update_TokenNotReadInThisScope_ShouldThrow(DatabaseProvider provider)
  {
    var seed = await AddRepositoriesAsync(database, provider, Ct);
    await using var context = seed.Organizations.ContextOf(seed.Organizations.FirstId);

    await Should.ThrowAsync<InvalidOperationException>(() => new UploadTokenRepository(context).UpdateAsync(OrganizationToken(Hash), Ct));
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task FindByHash_ShouldFindTheTokenOfTheOrganizationOnly(DatabaseProvider provider)
  {
    var seed = await AddRepositoriesAsync(database, provider, Ct);
    var token = OrganizationToken(Hash);

    await using (var writer = seed.Organizations.ContextOf(seed.Organizations.FirstId))
    {
      await new UploadTokenRepository(writer).TryAddAsync(token, Ct);
    }

    await using var first = seed.Organizations.ContextOf(seed.Organizations.FirstId);
    await using var second = seed.Organizations.ContextOf(seed.Organizations.SecondId);

    (await new UploadTokenRepository(first).FindByHashAsync(Hash, Ct)).ShouldNotBeNull().Id.ShouldBe(token.Id);
    (await new UploadTokenRepository(second).FindByHashAsync(Hash, Ct)).ShouldBeNull();
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task RecordUse_WithinAMinute_ShouldKeepTheFirstDate(DatabaseProvider provider)
  {
    var seed = await AddRepositoriesAsync(database, provider, Ct);
    var token = OrganizationToken(Hash);

    await using (var writer = seed.Organizations.ContextOf(seed.Organizations.FirstId))
    {
      await new UploadTokenRepository(writer).TryAddAsync(token, Ct);
      await new UploadTokenRepository(writer).RecordUseAsync(token.Id, Now, Ct);
      await new UploadTokenRepository(writer).RecordUseAsync(token.Id, Now.AddSeconds(30), Ct);
    }

    await using (var reader = seed.Organizations.ContextOf(seed.Organizations.FirstId))
    {
      (await new UploadTokenRepository(reader).FindByHashAsync(Hash, Ct)).ShouldNotBeNull().LastUsedAt.ShouldBe(Now);
    }

    await using var late = seed.Organizations.ContextOf(seed.Organizations.FirstId);
    await new UploadTokenRepository(late).RecordUseAsync(token.Id, Now.AddSeconds(61), Ct);

    await using var last = seed.Organizations.ContextOf(seed.Organizations.FirstId);
    (await new UploadTokenRepository(last).FindByHashAsync(Hash, Ct)).ShouldNotBeNull().LastUsedAt.ShouldBe(Now.AddSeconds(61));
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task RecordUse_OtherOrganization_ShouldChangeNothing(DatabaseProvider provider)
  {
    var seed = await AddRepositoriesAsync(database, provider, Ct);
    var token = OrganizationToken(Hash);

    await using (var writer = seed.Organizations.ContextOf(seed.Organizations.FirstId))
    {
      await new UploadTokenRepository(writer).TryAddAsync(token, Ct);
    }

    await using (var other = seed.Organizations.ContextOf(seed.Organizations.SecondId))
    {
      await new UploadTokenRepository(other).RecordUseAsync(token.Id, Now, Ct);
    }

    await using var reader = seed.Organizations.ContextOf(seed.Organizations.FirstId);
    (await new UploadTokenRepository(reader).FindByHashAsync(Hash, Ct)).ShouldNotBeNull().LastUsedAt.ShouldBeNull();
  }

  private static UploadToken OrganizationToken(byte[] hash) =>
    UploadToken.Create(UploadTokenScope.Organization, null, "ci", "hint", hash, false, "octocat", Now, null);

  #endregion
}
