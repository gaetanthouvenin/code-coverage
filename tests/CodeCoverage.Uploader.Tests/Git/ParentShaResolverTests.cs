// ------------------------------------------------------------------------------------------------
// <copyright file="ParentShaResolverTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Uploader.Git;

namespace CodeCoverage.Uploader.Tests.Git;

public sealed class ParentShaResolverTests
{
  #region Methods

  [Fact]
  public void Resolve_Shallow_ShouldReturnNull()
  {
    using var repository = new TemporaryGitRepository();
    repository.Commit("a.txt");
    repository.Commit("b.txt");
    using var clone = repository.CloneWithDepthOne();

    ParentShaResolver.Resolve(clone.Git).ShouldBeNull();
  }

  [Fact]
  public void Resolve_MergeHead_ShouldReturnNull()
  {
    using var repository = new TemporaryGitRepository();
    repository.Commit("a.txt");
    repository.Run("checkout", "-b", "feature");
    repository.Commit("b.txt");
    repository.Run("checkout", "main");
    repository.Commit("c.txt");
    repository.MergeNoFastForward("feature");

    ParentShaResolver.Resolve(repository.Git).ShouldBeNull();
  }

  [Fact]
  public void Resolve_SingleParent_ShouldReturnIt()
  {
    using var repository = new TemporaryGitRepository();
    var parent = repository.Commit("a.txt");
    repository.Commit("b.txt");

    ParentShaResolver.Resolve(repository.Git).ShouldBe(parent);
  }

  [Fact]
  public void Resolve_RootCommit_ShouldReturnNull()
  {
    using var repository = new TemporaryGitRepository();
    repository.Commit("a.txt");

    ParentShaResolver.Resolve(repository.Git).ShouldBeNull();
  }

  [Fact]
  public void Resolve_GitFails_ShouldReturnNull()
  {
    ParentShaResolver.Resolve(static _ => null).ShouldBeNull();
  }

  #endregion
}
