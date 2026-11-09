// ------------------------------------------------------------------------------------------------
// <copyright file="GitCommitTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Commits;
using CodeCoverage.Domain.Contracts.Forges;
using CodeCoverage.Domain.Git;
using CodeCoverage.Domain.Organizations;

namespace CodeCoverage.Domain.Tests.Commits;

public sealed class GitCommitTests
{
  #region Fields

  private static readonly GitSha Sha = GitSha.Parse("a94a8fe5ccb19ba61c4c0873d391e987982fbbd3");

  private static readonly GitSha Parent = GitSha.Parse("b94a8fe5ccb19ba61c4c0873d391e987982fbbd3");

  private static readonly DateTimeOffset Now = new(2026, 11, 9, 8, 0, 0, TimeSpan.Zero);

  #endregion

  #region Methods

  [Fact]
  public void Create_ShouldHoldWhatTheForgeReported()
  {
    var commit = GitCommit.Create(1, Info("message"), Now.AddHours(1));

    commit.RepositoryId.ShouldBe(1);
    commit.Sha.ShouldBe(Sha);
    commit.ParentShas.ShouldBe([Parent]);
    commit.Message.ShouldBe("message");
    commit.AuthorLogin.ShouldBe("author");
    commit.CommittedAt.ShouldBe(Now);
    commit.FetchedAt.ShouldBe(Now.AddHours(1));
  }

  [Fact]
  public void Create_ShouldCopyTheParents()
  {
    var parents = new List<GitSha> { Parent };
    var commit = GitCommit.Create(1, new ForgeCommitInfo(Sha, parents, "m", null, Now), Now);

    parents.Clear();

    commit.ParentShas.ShouldBe([Parent]);
  }

  [Fact]
  public void Create_MessageOverTheLimit_ShouldCutIt()
  {
    var commit = GitCommit.Create(1, Info(new string('a', GitCommit.MessageMaxLength + 10)), Now);

    commit.Message.Length.ShouldBe(GitCommit.MessageMaxLength);
  }

  // PostgreSQL refuses a text that holds half of a surrogate pair.
  [Fact]
  public void Create_LimitInsideASurrogatePair_ShouldNotSplitIt()
  {
    var message = new string('a', GitCommit.MessageMaxLength - 1) + "😀";

    var commit = GitCommit.Create(1, Info(message), Now);

    commit.Message.ShouldBe(new string('a', GitCommit.MessageMaxLength - 1));
  }

  [Fact]
  public void Create_AuthorOverTheLimit_ShouldThrow()
  {
    var info = new ForgeCommitInfo(Sha, [], "m", new string('a', Organization.LoginMaxLength + 1), Now);

    Should.Throw<ArgumentException>(() => GitCommit.Create(1, info, Now));
  }

  [Fact]
  public void Create_RepositoryNotPositive_ShouldThrow()
  {
    Should.Throw<ArgumentOutOfRangeException>(() => GitCommit.Create(0, Info("m"), Now));
  }

  private static ForgeCommitInfo Info(string message) => new(Sha, [Parent], message, "author", Now);

  #endregion
}
