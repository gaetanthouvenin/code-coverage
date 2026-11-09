// ------------------------------------------------------------------------------------------------
// <copyright file="RepositoryTreeRepositoryTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Application.Exceptions;
using CodeCoverage.Application.Features.Repositories;
using CodeCoverage.Domain.Contracts.Forges;
using CodeCoverage.Domain.Git;
using CodeCoverage.Infrastructure.Persistence;
using CodeCoverage.Infrastructure.Persistence.Repositories;
using CodeCoverage.Infrastructure.Tests.Support;

using static CodeCoverage.Infrastructure.Tests.Support.RepositorySeeder;

namespace CodeCoverage.Infrastructure.Tests.Persistence.Repositories;

// Every method against two organizations, on both engines: the rows of the other one are neither read nor written.
[Collection(DatabaseCollection.Name)]
public sealed class RepositoryTreeRepositoryTests(DatabaseFixture database)
{
  #region Fields

  private static readonly GitSha Blob = GitSha.Parse("d94a8fe5ccb19ba61c4c0873d391e987982fbbd3");

  #endregion

  #region Properties

  private static CancellationToken Ct => TestContext.Current.CancellationToken;

  #endregion

  #region Methods

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task Add_ShouldRestoreTheFiles(DatabaseProvider provider)
  {
    var seed = await AddRepositoriesAsync(database, provider, Ct);
    ForgeTreeEntry[] files = [new("src/é a.cs", Blob, false), new("b\nc.cs", Head, false)];

    await using (var writer = seed.Organizations.ContextOf(seed.Organizations.FirstId))
    {
      await new RepositoryTreeRepository(writer).AddAsync(seed.FirstRepository, new ForgeTree(Head, files, true), Now, Ct);
    }

    await using var reader = seed.Organizations.ContextOf(seed.Organizations.FirstId);
    var tree = (await new RepositoryTreeRepository(reader).FindAsync(seed.FirstRepository, Head, Ct)).ShouldNotBeNull();

    tree.Sha.ShouldBe(Head);
    tree.Truncated.ShouldBeTrue();
    tree.Entries.ShouldBe(files);
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task Find_OtherOrganization_ShouldReturnNull(DatabaseProvider provider)
  {
    var seed = await AddRepositoriesAsync(database, provider, Ct);

    await using (var writer = seed.Organizations.ContextOf(seed.Organizations.FirstId))
    {
      await new RepositoryTreeRepository(writer).AddAsync(seed.FirstRepository, new ForgeTree(Head, [], false), Now, Ct);
    }

    await using var other = seed.Organizations.ContextOf(seed.Organizations.SecondId);

    (await new RepositoryTreeRepository(other).FindAsync(seed.FirstRepository, Head, Ct)).ShouldBeNull();
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task Add_RepositoryOfOtherOrganization_ShouldThrowNotFound(DatabaseProvider provider)
  {
    var seed = await AddRepositoriesAsync(database, provider, Ct);
    await using var other = seed.Organizations.ContextOf(seed.Organizations.SecondId);

    var exception = await Should.ThrowAsync<NotFoundException>(
      () => new RepositoryTreeRepository(other).AddAsync(seed.FirstRepository, new ForgeTree(Head, [], false), Now, Ct)
    );

    exception.ErrorCode.ShouldBe(RepositoryErrors.NotFound);
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task Add_Twice_ShouldKeepTheFirstRowAndLeaveTheScopeUsable(DatabaseProvider provider)
  {
    var seed = await AddRepositoriesAsync(database, provider, Ct);
    await using var context = seed.Organizations.ContextOf(seed.Organizations.FirstId);
    var trees = new RepositoryTreeRepository(context);

    await trees.AddAsync(seed.FirstRepository, new ForgeTree(Head, [new ForgeTreeEntry("a.cs", Blob, false)], false), Now, Ct);
    await trees.AddAsync(seed.FirstRepository, new ForgeTree(Head, [], true), Now, Ct);
    await trees.AddAsync(seed.FirstRepository, new ForgeTree(NewHead, [], false), Now, Ct);

    var tree = (await trees.FindAsync(seed.FirstRepository, Head, Ct)).ShouldNotBeNull();

    tree.Truncated.ShouldBeFalse();
    tree.Entries.ShouldHaveSingleItem();
  }

  #endregion
}
