// ------------------------------------------------------------------------------------------------
// <copyright file="PullRequestTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Contracts.Forges;
using CodeCoverage.Domain.Git;
using CodeCoverage.Domain.PullRequests;

namespace CodeCoverage.Domain.Tests.PullRequests;

public sealed class PullRequestTests
{
  #region Fields

  private static readonly GitSha Head = GitSha.Parse("a94a8fe5ccb19ba61c4c0873d391e987982fbbd3");

  private static readonly GitSha Base = GitSha.Parse("b94a8fe5ccb19ba61c4c0873d391e987982fbbd3");

  private static readonly DateTimeOffset Now = new(2026, 11, 9, 8, 0, 0, TimeSpan.Zero);

  #endregion

  #region Methods

  [Fact]
  public void Create_ShouldHoldWhatTheForgeReports()
  {
    var pull = PullRequest.Create(5, Info(ForgePullState.Open), Now);

    pull.RepositoryId.ShouldBe(5);
    pull.Number.ShouldBe(7);
    pull.State.ShouldBe(PullRequestState.Open);
    pull.Title.ShouldBe("title");
    pull.AuthorLogin.ShouldBe("octocat");
    pull.HeadSha.ShouldBe(Head);
    pull.HeadBranch.ShouldBe("feature");
    pull.BaseBranch.ShouldBe("main");
    pull.BaseShaGitHub.ShouldBe(Base);
    pull.UpdatedAt.ShouldBe(Now);
    pull.Version.ShouldBe(0);
    pull.AcceptsComments.ShouldBeTrue();
  }

  [Fact]
  public void Close_Merged_ShouldStopComments()
  {
    var pull = PullRequest.Create(5, Info(ForgePullState.Open), Now);

    pull.Close(merged: true, Now.AddMinutes(1));

    pull.State.ShouldBe(PullRequestState.Merged);
    pull.AcceptsComments.ShouldBeFalse();
    pull.UpdatedAt.ShouldBe(Now.AddMinutes(1));
    pull.Version.ShouldBe(1);
  }

  [Fact]
  public void Close_NotMerged_ShouldStopComments()
  {
    var pull = PullRequest.Create(5, Info(ForgePullState.Open), Now);

    pull.Close(merged: false, Now);

    pull.State.ShouldBe(PullRequestState.Closed);
    pull.AcceptsComments.ShouldBeFalse();
  }

  [Fact]
  public void Close_Twice_ShouldChangeNothingTheSecondTime()
  {
    var pull = PullRequest.Create(5, Info(ForgePullState.Open), Now);

    pull.Close(merged: true, Now);
    pull.Close(merged: true, Now.AddDays(1));

    pull.UpdatedAt.ShouldBe(Now);
    pull.Version.ShouldBe(1);
  }

  [Fact]
  public void Refresh_SameContent_ShouldChangeNothing()
  {
    var pull = PullRequest.Create(5, Info(ForgePullState.Open), Now);

    pull.Refresh(Info(ForgePullState.Open), Now.AddDays(1));

    pull.Version.ShouldBe(0);
    pull.UpdatedAt.ShouldBe(Now);
  }

  [Fact]
  public void Refresh_NewHead_ShouldApplyItAndIncrementTheVersion()
  {
    var pull = PullRequest.Create(5, Info(ForgePullState.Open), Now);

    pull.Refresh(Info(ForgePullState.Open) with { HeadSha = Base }, Now.AddDays(1));

    pull.HeadSha.ShouldBe(Base);
    pull.Version.ShouldBe(1);
    pull.UpdatedAt.ShouldBe(Now.AddDays(1));
  }

  [Fact]
  public void Refresh_Reopened_ShouldAcceptCommentsAgain()
  {
    var pull = PullRequest.Create(5, Info(ForgePullState.Closed), Now);

    pull.Refresh(Info(ForgePullState.Open), Now);

    pull.AcceptsComments.ShouldBeTrue();
  }

  [Fact]
  public void Refresh_OtherNumber_ShouldThrow()
  {
    var pull = PullRequest.Create(5, Info(ForgePullState.Open), Now);

    Should.Throw<ArgumentException>(() => pull.Refresh(Info(ForgePullState.Open) with { Number = 8 }, Now));
  }

  [Fact]
  public void Create_TitleTooLong_ShouldBeCut()
  {
    var pull = PullRequest.Create(5, Info(ForgePullState.Open) with { Title = new string('t', PullRequest.TitleMaxLength + 5) }, Now);

    pull.Title.Length.ShouldBe(PullRequest.TitleMaxLength);
  }

  [Fact]
  public void Create_BranchTooLong_ShouldThrow()
  {
    Should.Throw<ArgumentException>(
      () => PullRequest.Create(5, Info(ForgePullState.Open) with { HeadBranch = new string('b', PullRequest.BranchMaxLength + 1) }, Now)
    );
  }

  [Fact]
  public void Create_WithoutAuthor_ShouldKeepNone()
  {
    PullRequest.Create(5, Info(ForgePullState.Open) with { AuthorLogin = null }, Now).AuthorLogin.ShouldBeNull();
  }

  [Fact]
  public void RecordComment_ShouldKeepTheCommentItsHashAndItsHead()
  {
    var pull = PullRequest.Create(5, Info(ForgePullState.Open), Now);
    var hash = Hash(1);

    pull.RecordComment(42, hash, Head, Now.AddHours(1));

    pull.CommentId.ShouldBe(42);
    pull.CommentBodyHash.ShouldBe(hash);
    pull.CommentBodyHash.ShouldNotBeSameAs(hash, "the hash is copied");
    pull.CommentedHeadSha.ShouldBe(Head);
    pull.UpdatedAt.ShouldBe(Now.AddHours(1));
    pull.Version.ShouldBe(1);
    pull.HasCommentBody(Hash(1)).ShouldBeTrue();
    pull.HasCommentBody(Hash(2)).ShouldBeFalse();
  }

  [Fact]
  public void RecordComment_SameComment_ShouldChangeNothing()
  {
    var pull = PullRequest.Create(5, Info(ForgePullState.Open), Now);
    pull.RecordComment(42, Hash(1), Head, Now);

    pull.RecordComment(42, Hash(1), Head, Now.AddHours(1));
    pull.Version.ShouldBe(1);

    pull.RecordComment(42, Hash(2), Head, Now.AddHours(1));
    pull.Version.ShouldBe(2);

    pull.RecordComment(43, Hash(2), Head, Now.AddHours(1));
    pull.Version.ShouldBe(3);

    pull.RecordComment(43, Hash(2), Base, Now.AddHours(1));
    pull.Version.ShouldBe(4);
  }

  [Fact]
  public void RecordComment_InvalidArguments_ShouldThrow()
  {
    var pull = PullRequest.Create(5, Info(ForgePullState.Open), Now);

    Should.Throw<ArgumentOutOfRangeException>(() => pull.RecordComment(0, Hash(1), Head, Now));
    Should.Throw<ArgumentNullException>(() => pull.RecordComment(1, null!, Head, Now));
    Should.Throw<ArgumentNullException>(() => pull.RecordComment(1, Hash(1), null!, Now));
    Should.Throw<ArgumentException>(() => pull.RecordComment(1, new byte[31], Head, Now));
    Should.Throw<ArgumentNullException>(() => pull.HasCommentBody(null!));
  }

  [Fact]
  public void HasCommentBody_WithoutAComment_ShouldBeFalse()
  {
    PullRequest.Create(5, Info(ForgePullState.Open), Now).HasCommentBody(Hash(1)).ShouldBeFalse();
  }

  [Fact]
  public void RecordBase_ShouldCountOnlyAChange()
  {
    var pull = PullRequest.Create(5, Info(ForgePullState.Open), Now);

    pull.RecordBase(null, null, Now.AddHours(1));
    pull.Version.ShouldBe(0);

    pull.RecordBase(Base, null, Now.AddHours(1));
    pull.ForkPointSha.ShouldBe(Base);
    pull.ComparedToSha.ShouldBeNull();
    pull.Version.ShouldBe(1);

    pull.RecordBase(Base, Base, Now.AddHours(2));
    pull.ComparedToSha.ShouldBe(Base);
    pull.UpdatedAt.ShouldBe(Now.AddHours(2));
    pull.Version.ShouldBe(2);

    pull.RecordBase(Base, Base, Now.AddHours(3));
    pull.Version.ShouldBe(2);
  }

  private static byte[] Hash(byte value)
  {
    var hash = new byte[PullRequest.CommentBodyHashLength];

    hash[0] = value;

    return hash;
  }

  [Fact]
  public void CanRecord_ShouldRefuseOnlyABranchThatDoesNotFit()
  {
    var tooLong = new string('b', PullRequest.BranchMaxLength + 1);
    var longest = new string('b', PullRequest.BranchMaxLength);

    PullRequest.CanRecord(Info(ForgePullState.Open)).ShouldBeTrue();
    PullRequest.CanRecord(Info(ForgePullState.Open) with { HeadBranch = longest, BaseBranch = longest }).ShouldBeTrue();
    PullRequest.CanRecord(Info(ForgePullState.Open) with { HeadBranch = tooLong }).ShouldBeFalse();
    PullRequest.CanRecord(Info(ForgePullState.Open) with { BaseBranch = tooLong }).ShouldBeFalse();
    Should.Throw<ArgumentNullException>(() => PullRequest.CanRecord(null!));
  }

  private static ForgePullInfo Info(ForgePullState state) => new(7, state, "title", "octocat", Head, "feature", "main", Base);

  #endregion
}
