// ------------------------------------------------------------------------------------------------
// <copyright file="NetworkCollectorTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Uploader.Git;

namespace CodeCoverage.Uploader.Tests.Git;

public sealed class NetworkCollectorTests
{
  #region Methods

  // The acceptance of the micro-feature: a clone of depth 1 lists every file, a name that git would quote included.
  [Fact]
  public void Collect_DepthOneClone_ShouldListEveryFile()
  {
    using var repository = new TemporaryGitRepository();
    repository.Commit("a.txt");
    repository.Commit("é b.txt");
    using var clone = repository.CloneWithDepthOne();

    NetworkCollector.Collect(clone.GitRaw).ShouldBe(["a.txt", "é b.txt"], ignoreOrder: true);
  }

  [Fact]
  public void Collect_GitFails_ShouldReturnNothing()
  {
    NetworkCollector.Collect(static _ => null).ShouldBeEmpty();
  }

  [Fact]
  public void Collect_ShouldSplitOnTheNulCharacter()
  {
    NetworkCollector.Collect(static _ => "a\nb.txt\0c.txt\0").ShouldBe(["a\nb.txt", "c.txt"]);
  }

  #endregion
}
