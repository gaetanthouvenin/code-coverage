// ------------------------------------------------------------------------------------------------
// <copyright file="DiffCacheRepositoryTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Application.Exceptions;
using CodeCoverage.Application.Features.Repositories;
using CodeCoverage.Domain.Diffs;
using CodeCoverage.Domain.Git;
using CodeCoverage.Infrastructure.Persistence;
using CodeCoverage.Infrastructure.Persistence.Repositories;
using CodeCoverage.Infrastructure.Tests.Support;

using static CodeCoverage.Infrastructure.Tests.Support.RepositorySeeder;

namespace CodeCoverage.Infrastructure.Tests.Persistence.Repositories;

// Every method against two organizations, on both engines: the rows of the other one are neither read nor written.
[Collection(DatabaseCollection.Name)]
public sealed class DiffCacheRepositoryTests(DatabaseFixture database)
{
  #region Fields

  private const string Patch = "@@ -1,1 +1,2 @@\n line\n+added";

  private static readonly GitSha Base = GitSha.Parse("c94a8fe5ccb19ba61c4c0873d391e987982fbbd3");

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
    var written = new DiffSet(
      [FileDiff.Create("src/é a.cs", "old.cs", FileDiffStatus.Renamed, Patch), FileDiff.Create("binary.png", null, FileDiffStatus.Added, null)],
      Truncated: true,
      DiffSource.PullFiles
    );

    await using (var writer = seed.Organizations.ContextOf(seed.Organizations.FirstId))
    {
      await new DiffCacheRepository(writer).AddAsync(seed.FirstRepository, Base, Head, written, Now, Ct);
    }

    await using var reader = seed.Organizations.ContextOf(seed.Organizations.FirstId);
    var diff = (await new DiffCacheRepository(reader).FindAsync(seed.FirstRepository, Base, Head, DiffSource.PullFiles, Ct)).ShouldNotBeNull();

    diff.Truncated.ShouldBeTrue();
    diff.Source.ShouldBe(DiffSource.PullFiles);
    diff.Files.Count.ShouldBe(2);
    diff.Files[0].Path.ShouldBe("src/é a.cs");
    diff.Files[0].PreviousPath.ShouldBe("old.cs");
    diff.Files[0].Status.ShouldBe(FileDiffStatus.Renamed);
    diff.Files[0].AddedLines.ShouldBe([2]);
    diff.Files[1].HasPatch.ShouldBeFalse();
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task Find_OtherHeadBaseOrKind_ShouldReturnNull(DatabaseProvider provider)
  {
    var seed = await AddRepositoriesAsync(database, provider, Ct);
    await using var context = seed.Organizations.ContextOf(seed.Organizations.FirstId);
    var cache = new DiffCacheRepository(context);

    await cache.AddAsync(seed.FirstRepository, Base, Head, Diff(DiffSource.Compare), Now, Ct);

    (await cache.FindAsync(seed.FirstRepository, Base, NewHead, DiffSource.Compare, Ct)).ShouldBeNull();
    (await cache.FindAsync(seed.FirstRepository, NewHead, Head, DiffSource.Compare, Ct)).ShouldBeNull();
    (await cache.FindAsync(seed.FirstRepository, Base, Head, DiffSource.PullFiles, Ct)).ShouldBeNull();
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task Find_OtherOrganization_ShouldReturnNull(DatabaseProvider provider)
  {
    var seed = await AddRepositoriesAsync(database, provider, Ct);

    await using (var writer = seed.Organizations.ContextOf(seed.Organizations.FirstId))
    {
      await new DiffCacheRepository(writer).AddAsync(seed.FirstRepository, Base, Head, Diff(DiffSource.Compare), Now, Ct);
    }

    await using var other = seed.Organizations.ContextOf(seed.Organizations.SecondId);

    (await new DiffCacheRepository(other).FindAsync(seed.FirstRepository, Base, Head, DiffSource.Compare, Ct)).ShouldBeNull();
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task Add_RepositoryOfOtherOrganization_ShouldThrowNotFound(DatabaseProvider provider)
  {
    var seed = await AddRepositoriesAsync(database, provider, Ct);
    await using var other = seed.Organizations.ContextOf(seed.Organizations.SecondId);

    var exception = await Should.ThrowAsync<NotFoundException>(
      () => new DiffCacheRepository(other).AddAsync(seed.FirstRepository, Base, Head, Diff(DiffSource.Compare), Now, Ct)
    );

    exception.ErrorCode.ShouldBe(RepositoryErrors.NotFound);
  }

  // Immutable for a base and a head: the first row stays, and the scope can still save.
  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task Add_Twice_ShouldKeepTheFirstRowAndLeaveTheScopeUsable(DatabaseProvider provider)
  {
    var seed = await AddRepositoriesAsync(database, provider, Ct);
    await using var context = seed.Organizations.ContextOf(seed.Organizations.FirstId);
    var cache = new DiffCacheRepository(context);

    await cache.AddAsync(seed.FirstRepository, Base, Head, Diff(DiffSource.Compare, truncated: false), Now, Ct);
    await cache.AddAsync(seed.FirstRepository, Base, Head, Diff(DiffSource.Compare, truncated: true), Now, Ct);
    await cache.AddAsync(seed.FirstRepository, Base, NewHead, Diff(DiffSource.Compare), Now, Ct);

    (await cache.FindAsync(seed.FirstRepository, Base, Head, DiffSource.Compare, Ct)).ShouldNotBeNull().Truncated.ShouldBeFalse();
    (await cache.FindAsync(seed.FirstRepository, Base, NewHead, DiffSource.Compare, Ct)).ShouldNotBeNull();
  }

  private static DiffSet Diff(DiffSource source, bool truncated = false) =>
    new([FileDiff.Create("a.cs", null, FileDiffStatus.Modified, Patch)], truncated, source);

  #endregion
}
