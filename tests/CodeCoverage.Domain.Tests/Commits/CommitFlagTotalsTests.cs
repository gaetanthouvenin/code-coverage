// ------------------------------------------------------------------------------------------------
// <copyright file="CommitFlagTotalsTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Commits;
using CodeCoverage.Domain.Coverage;
using CodeCoverage.Domain.Git;

namespace CodeCoverage.Domain.Tests.Commits;

public sealed class CommitFlagTotalsTests
{
  #region Fields

  private static readonly GitSha Sha = GitSha.Parse("a94a8fe5ccb19ba61c4c0873d391e987982fbbd3");

  private static readonly DateTimeOffset Now = new(2026, 11, 9, 8, 0, 0, TimeSpan.Zero);

  private static readonly CoverageTotals Totals = new(6, 3, 1, 2, 1);

  #endregion

  #region Methods

  [Fact]
  public void Create_ProjectRow_ShouldRepeatTheCommit()
  {
    var commit = Finalized();

    var totals = CommitFlagTotals.Create(commit, CommitFlagTotals.ProjectFlag, Totals, false, null);

    totals.Flag.ShouldBeEmpty();
    totals.RepositoryId.ShouldBe(5);
    totals.Branch.ShouldBe("main");
    totals.CompletedAt.ShouldBe(Now);
    totals.Lines.ShouldBe(10);
    totals.Hits.ShouldBe(6);
    totals.Misses.ShouldBe(3);
    totals.Partials.ShouldBe(1);
    totals.Coverage.ShouldBe(60m);
    totals.IsCarriedForward.ShouldBeFalse();
  }

  [Fact]
  public void Create_LongestFlag_ShouldBeAccepted()
  {
    CommitFlagTotals.Create(Finalized(), new string('f', CommitFlagTotals.FlagMaxLength), Totals, false, null)
                    .Flag.Length.ShouldBe(CommitFlagTotals.FlagMaxLength);
  }

  [Fact]
  public void Create_FlagTooLong_ShouldThrow()
  {
    Should.Throw<ArgumentException>(() => CommitFlagTotals.Create(Finalized(), new string('f', CommitFlagTotals.FlagMaxLength + 1), Totals, false, null))
          .ParamName.ShouldBe("flag");
  }

  [Fact]
  public void Create_WhiteSpaceFlag_ShouldThrow()
  {
    Should.Throw<ArgumentException>(() => CommitFlagTotals.Create(Finalized(), " ", Totals, false, null)).ParamName.ShouldBe("flag");
  }

  [Fact]
  public void Create_CommitNotFinalized_ShouldThrow()
  {
    Should.Throw<ArgumentException>(() => CommitFlagTotals.Create(Commit.Create(5, Sha, Now), "unit", Totals, false, null))
          .ParamName.ShouldBe("commit");
  }

  [Fact]
  public void Create_OriginOnAFlagNotCarriedForward_ShouldThrow()
  {
    Should.Throw<ArgumentException>(() => CommitFlagTotals.Create(Finalized(), "unit", Totals, false, Sha))
          .ParamName.ShouldBe("carriedForwardFromSha");
  }

  [Fact]
  public void Create_CarriedForward_ShouldKeepTheOrigin()
  {
    var totals = CommitFlagTotals.Create(Finalized(), "unit", Totals, true, Sha);

    totals.IsCarriedForward.ShouldBeTrue();
    totals.CarriedForwardFromSha.ShouldBe(Sha);
  }

  private static Commit Finalized()
  {
    var commit = Commit.Create(5, Sha, Now);
    commit.MarkPending("main", null, Now);
    commit.RecordFinalization(CommitState.Complete, 1, Totals, 1, 1, CarryforwardStatus.None, "default", Now);

    return commit;
  }

  #endregion
}
