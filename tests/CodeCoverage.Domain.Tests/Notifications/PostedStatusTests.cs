// ------------------------------------------------------------------------------------------------
// <copyright file="PostedStatusTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Notifications;

namespace CodeCoverage.Domain.Tests.Notifications;

public sealed class PostedStatusTests
{
  #region Fields

  private const string Url = "https://coverage.example.com/commit";

  private static readonly CommitStatus Status = new("coverage/project", StatusState.Success, "ok");

  #endregion

  #region Methods

  [Fact]
  public void Create_LocalTime_ShouldBeUtc()
  {
    var local = new DateTimeOffset(2026, 11, 10, 10, 0, 0, TimeSpan.FromHours(2));

    var posted = new PostedStatus(9, Status, Url, local);

    posted.PostedAt.Offset.ShouldBe(TimeSpan.Zero);
    posted.PostedAt.ShouldBe(local);
    posted.CommitId.ShouldBe(9);
    posted.Status.ShouldBe(Status);
    posted.TargetUrl.ShouldBe(Url);
  }

  [Theory]
  [InlineData(0)]
  [InlineData(-1)]
  public void Create_CommitNotPositive_ShouldThrow(long commitId)
  {
    Should.Throw<ArgumentOutOfRangeException>(() => new PostedStatus(commitId, Status, Url, DateTimeOffset.UnixEpoch));
  }

  [Fact]
  public void Create_ContextAtTheLimit_ShouldPassAndOneMoreShouldThrow()
  {
    var atLimit = new CommitStatus(new string('c', PostedStatus.ContextMaxLength), StatusState.Success, "ok");
    var over = new CommitStatus(new string('c', PostedStatus.ContextMaxLength + 1), StatusState.Success, "ok");

    new PostedStatus(9, atLimit, Url, DateTimeOffset.UnixEpoch).Status.ShouldBe(atLimit);
    Should.Throw<ArgumentException>(() => new PostedStatus(9, over, Url, DateTimeOffset.UnixEpoch));
  }

  [Theory]
  [InlineData("")]
  [InlineData(" ")]
  public void Create_EmptyTargetUrl_ShouldThrow(string url)
  {
    Should.Throw<ArgumentException>(() => new PostedStatus(9, Status, url, DateTimeOffset.UnixEpoch));
  }

  [Fact]
  public void Create_WithoutTargetUrl_ShouldKeepNone()
  {
    new PostedStatus(9, Status, null, DateTimeOffset.UnixEpoch).TargetUrl.ShouldBeNull();
  }

  [Fact]
  public void Create_TargetUrlOverTheLimit_ShouldThrow()
  {
    Should.Throw<ArgumentException>(() => new PostedStatus(9, Status, "https://x/" + new string('a', PostedStatus.TargetUrlMaxLength), DateTimeOffset.UnixEpoch));
  }

  [Fact]
  public void Create_NullStatus_ShouldThrow()
  {
    Should.Throw<ArgumentNullException>(() => new PostedStatus(9, null!, Url, DateTimeOffset.UnixEpoch));
  }

  #endregion
}