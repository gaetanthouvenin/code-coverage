// ------------------------------------------------------------------------------------------------
// <copyright file="CommitConfigRepositoryTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Application.Exceptions;
using CodeCoverage.Application.Features.Repositories;
using CodeCoverage.Domain.Config;
using CodeCoverage.Infrastructure.Persistence;
using CodeCoverage.Infrastructure.Persistence.Repositories;
using CodeCoverage.Infrastructure.Tests.Support;

using static CodeCoverage.Infrastructure.Tests.Support.RepositorySeeder;

namespace CodeCoverage.Infrastructure.Tests.Persistence.Repositories;

// Every method against two organizations, on both engines: the rows of the other one are neither read nor written.
[Collection(DatabaseCollection.Name)]
public sealed class CommitConfigRepositoryTests(DatabaseFixture database)
{
  #region Properties

  private static CancellationToken Ct => TestContext.Current.CancellationToken;

  #endregion

  #region Methods

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task Add_ShouldStoreTheRow(DatabaseProvider provider)
  {
    var seed = await AddRepositoriesAsync(database, provider, Ct);
    var config = CommitConfig.Create(
      seed.FirstRepository,
      Head,
      ConfigSource.Commit,
      ".github/codecov.yml",
      "coverage: {}",
      [new ConfigWarning(ConfigWarning.InvalidValue, "coverage.range", "x")],
      ["broken"],
      Now
    );

    await using (var writer = seed.Organizations.ContextOf(seed.Organizations.FirstId))
    {
      await new CommitConfigRepository(writer).AddAsync(config, Ct);
    }

    await using var reader = seed.Organizations.ContextOf(seed.Organizations.FirstId);
    var saved = (await new CommitConfigRepository(reader).FindAsync(seed.FirstRepository, Head, Ct)).ShouldNotBeNull();

    saved.Sha.ShouldBe(Head);
    saved.Source.ShouldBe(ConfigSource.Commit);
    saved.Path.ShouldBe(".github/codecov.yml");
    saved.RawYaml.ShouldBe("coverage: {}");
    saved.Warnings.ShouldHaveSingleItem().ShouldBe(new ConfigWarning(ConfigWarning.InvalidValue, "coverage.range", "x"));
    saved.Errors.ShouldBe(["broken"]);
    saved.FetchedAt.ShouldBe(Now);
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task Find_OtherOrganization_ShouldReturnNull(DatabaseProvider provider)
  {
    var seed = await AddRepositoriesAsync(database, provider, Ct);

    await using (var writer = seed.Organizations.ContextOf(seed.Organizations.FirstId))
    {
      await new CommitConfigRepository(writer).AddAsync(New(seed.FirstRepository, ConfigSource.Builtin), Ct);
    }

    await using var other = seed.Organizations.ContextOf(seed.Organizations.SecondId);

    (await new CommitConfigRepository(other).FindAsync(seed.FirstRepository, Head, Ct)).ShouldBeNull();
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task Add_RepositoryOfOtherOrganization_ShouldThrowNotFound(DatabaseProvider provider)
  {
    var seed = await AddRepositoriesAsync(database, provider, Ct);
    await using var other = seed.Organizations.ContextOf(seed.Organizations.SecondId);

    var exception = await Should.ThrowAsync<NotFoundException>(
      () => new CommitConfigRepository(other).AddAsync(New(seed.FirstRepository, ConfigSource.Builtin), Ct)
    );

    exception.ErrorCode.ShouldBe(RepositoryErrors.NotFound);
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task Add_Twice_ShouldKeepTheFirstRow(DatabaseProvider provider)
  {
    var seed = await AddRepositoriesAsync(database, provider, Ct);
    await using var context = seed.Organizations.ContextOf(seed.Organizations.FirstId);
    var configs = new CommitConfigRepository(context);

    await configs.AddAsync(New(seed.FirstRepository, ConfigSource.Builtin), Ct);
    await configs.AddAsync(New(seed.FirstRepository, ConfigSource.Organization), Ct);

    (await configs.FindAsync(seed.FirstRepository, Head, Ct)).ShouldNotBeNull().Source.ShouldBe(ConfigSource.Builtin);
  }

  private static CommitConfig New(long repositoryId, ConfigSource source) =>
    CommitConfig.Create(repositoryId, Head, source, null, null, [], [], Now);

  #endregion
}
