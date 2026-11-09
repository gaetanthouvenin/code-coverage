// ------------------------------------------------------------------------------------------------
// <copyright file="CommitTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Commits;
using CodeCoverage.Domain.Coverage;
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

  [Fact]
  public void RecordFinalization_ShouldStoreTheResultInANewRevision()
  {
    var commit = Commit.Create(1, Sha, Now);
    var later = Now.AddMinutes(5);

    commit.RecordFinalization(CommitState.Complete, 2, new CoverageTotals(6, 3, 1, 2, 1), 3, 2, CarryforwardStatus.Applied, "default-branch", later);

    commit.State.ShouldBe(CommitState.Complete);
    commit.Files.ShouldBe(2);
    commit.Lines.ShouldBe(10);
    commit.Hits.ShouldBe(6);
    commit.Misses.ShouldBe(3);
    commit.Partials.ShouldBe(1);
    commit.Branches.ShouldBe(2);
    commit.Methods.ShouldBe(1);
    commit.Coverage.ShouldBe(60m);
    commit.SessionCount.ShouldBe(3);
    commit.UploadedSessionCount.ShouldBe(2);
    commit.CarryforwardStatus.ShouldBe(CarryforwardStatus.Applied);
    commit.ConfigSource.ShouldBe("default-branch");
    commit.CompletedAt.ShouldBe(later);
    commit.UpdatedAt.ShouldBe(later);
    commit.ReportRevision.ShouldBe(1);
    commit.Version.ShouldBe(1);
  }

  [Fact]
  public void RecordFinalization_Twice_ShouldIncrementRevisionAndVersion()
  {
    var commit = Commit.Create(1, Sha, Now);

    commit.RecordFinalization(CommitState.Complete, 0, default, 0, 0, CarryforwardStatus.None, string.Empty, Now);
    commit.RecordFinalization(CommitState.Error, 0, default, 0, 0, CarryforwardStatus.None, string.Empty, Now);

    commit.State.ShouldBe(CommitState.Error);
    commit.ReportRevision.ShouldBe(2);
    commit.Version.ShouldBe(2);
  }

  [Theory]
  [InlineData(CommitState.Known)]
  [InlineData(CommitState.Pending)]
  [InlineData(CommitState.Compacted)]
  public void RecordFinalization_StateNotFinal_ShouldThrow(CommitState state)
  {
    var commit = Commit.Create(1, Sha, Now);

    Should.Throw<ArgumentOutOfRangeException>(() => commit.RecordFinalization(state, 0, default, 0, 0, CarryforwardStatus.None, string.Empty, Now))
          .ParamName.ShouldBe("state");
  }

  [Fact]
  public void RecordFinalization_MoreUploadedSessionsThanSessions_ShouldThrow()
  {
    var commit = Commit.Create(1, Sha, Now);

    Should.Throw<ArgumentOutOfRangeException>(() => commit.RecordFinalization(CommitState.Complete, 0, default, 1, 2, CarryforwardStatus.None, string.Empty, Now));
  }

  [Fact]
  public void RecordFinalization_ConfigSourceOverTheLimit_ShouldThrow()
  {
    var commit = Commit.Create(1, Sha, Now);
    var source = new string('a', Commit.ConfigSourceMaxLength + 1);

    Should.Throw<ArgumentException>(() => commit.RecordFinalization(CommitState.Complete, 0, default, 0, 0, CarryforwardStatus.None, source, Now))
          .ParamName.ShouldBe("configSource");
  }

  #endregion
}
