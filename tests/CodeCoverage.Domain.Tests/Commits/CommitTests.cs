// ------------------------------------------------------------------------------------------------
// <copyright file="CommitTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Commits;
using CodeCoverage.Domain.Git;

namespace CodeCoverage.Domain.Tests.Commits;

public sealed class CommitTests
{
  #region Fields

  private static readonly GitSha Sha = GitSha.Parse("a94a8fe5ccb19ba61c4c0873d391e987982fbbd3");

  private static readonly DateTimeOffset Now = new(2026, 11, 9, 8, 0, 0, TimeSpan.Zero);

  #endregion

  #region Methods

  [Fact]
  public void Create_ShouldBeKnownWithoutUpload()
  {
    var commit = Commit.Create(1, Sha, Now);

    commit.State.ShouldBe(CommitState.Known);
    commit.CarryforwardStatus.ShouldBe(CarryforwardStatus.None);
    commit.Sha.ShouldBe(Sha);
    commit.UploadSequence.ShouldBe(0);
    commit.ConfigSource.ShouldBeEmpty();
    commit.LastUploadAt.ShouldBeNull();
    commit.CreatedAt.ShouldBe(Now);
  }

  [Fact]
  public void Create_RepositoryNotPositive_ShouldThrow()
  {
    Should.Throw<ArgumentOutOfRangeException>(() => Commit.Create(0, Sha, Now));
  }

  [Fact]
  public void MarkPending_FromKnown_ShouldSetPending()
  {
    var commit = Commit.Create(1, Sha, Now);

    commit.MarkPending("main", 7, Now.AddHours(1));

    commit.State.ShouldBe(CommitState.Pending);
    commit.Branch.ShouldBe("main");
    commit.PullNumber.ShouldBe(7);
    commit.LastUploadAt.ShouldBe(Now.AddHours(1));
    commit.UpdatedAt.ShouldBe(Now.AddHours(1));
  }

  [Fact]
  public void MarkPending_Twice_ShouldKeepTheFirstBranchAndPullRequest()
  {
    var commit = Commit.Create(1, Sha, Now);

    commit.MarkPending("main", 7, Now);
    commit.MarkPending("other", 8, Now.AddHours(1));

    commit.State.ShouldBe(CommitState.Pending);
    commit.Branch.ShouldBe("main");
    commit.PullNumber.ShouldBe(7);
    commit.LastUploadAt.ShouldBe(Now.AddHours(1));
  }

  [Fact]
  public void MarkPending_WithoutBranchThenWithOne_ShouldFillIt()
  {
    var commit = Commit.Create(1, Sha, Now);

    commit.MarkPending(null, null, Now);
    commit.MarkPending("main", 7, Now.AddHours(1));

    commit.Branch.ShouldBe("main");
    commit.PullNumber.ShouldBe(7);
  }

  // The parallel uploads of a matrix all pass here: a change of version would make them refuse one another.
  [Fact]
  public void MarkPending_ShouldNotChangeTheVersion()
  {
    var commit = Commit.Create(1, Sha, Now);

    commit.MarkPending("main", 7, Now);

    commit.Version.ShouldBe(0);
  }

  [Theory]
  [InlineData("")]
  [InlineData(" ")]
  public void MarkPending_BlankBranch_ShouldThrow(string branch)
  {
    var commit = Commit.Create(1, Sha, Now);

    Should.Throw<ArgumentException>(() => commit.MarkPending(branch, null, Now));
  }

  [Fact]
  public void MarkPending_BranchOverTheLimit_ShouldThrow()
  {
    var commit = Commit.Create(1, Sha, Now);
    var branch = new string('a', Commit.BranchMaxLength + 1);

    Should.Throw<ArgumentException>(() => commit.MarkPending(branch, null, Now));
  }

  [Theory]
  [InlineData(0)]
  [InlineData(-1)]
  public void MarkPending_PullNumberNotPositive_ShouldThrow(int pullNumber)
  {
    var commit = Commit.Create(1, Sha, Now);

    Should.Throw<ArgumentOutOfRangeException>(() => commit.MarkPending("main", pullNumber, Now));
  }

  #endregion
}
