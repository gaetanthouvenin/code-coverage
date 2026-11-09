// ------------------------------------------------------------------------------------------------
// <copyright file="ProcessingLogTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Commits;

namespace CodeCoverage.Domain.Tests.Commits;

public sealed class ProcessingLogTests
{
  #region Methods

  [Fact]
  public void Empty_ShouldHoldNothing()
  {
    ProcessingLog.Empty.Messages.ShouldBeEmpty();
    ProcessingLog.Empty.Paths.ShouldBeEmpty();
    ProcessingLog.Empty.MessageTotal.ShouldBe(0);
    ProcessingLog.Empty.PathTotal.ShouldBe(0);
    ProcessingLog.Empty.IsTruncated.ShouldBeFalse();
  }

  [Fact]
  public void Add_WithinTheCap_ShouldKeepEveryMessageInOrder()
  {
    var log = ProcessingLog.Empty.Add("first").Add("second");

    log.Messages.ShouldBe(["first", "second"]);
    log.MessageTotal.ShouldBe(2);
    log.IsTruncated.ShouldBeFalse();
  }

  [Fact]
  public void Add_Over64Kb_ShouldTruncateKeepingTotal()
  {
    var message = new string('a', 1024);
    var log = ProcessingLog.Empty;

    for (var i = 0; i < 100; i++)
    {
      log = log.Add(message);
    }

    log.MessageTotal.ShouldBe(100);
    log.Messages.Count.ShouldBeLessThan(100);
    log.Messages.Count.ShouldBeGreaterThan(50);
    log.Messages.Sum(static m => m.Length).ShouldBeLessThanOrEqualTo(ProcessingLog.MaxBytes);
    log.IsTruncated.ShouldBeTrue();
  }

  [Fact]
  public void Add_MessageLargerThanTheCap_ShouldOnlyCountIt()
  {
    var log = ProcessingLog.Empty.Add(new string('a', ProcessingLog.MaxBytes + 1));

    log.Messages.ShouldBeEmpty();
    log.MessageTotal.ShouldBe(1);
    log.IsTruncated.ShouldBeTrue();
  }

  [Fact]
  public void Add_ShouldLeaveTheOriginalUntouched()
  {
    var original = ProcessingLog.Empty.Add("first");

    original.Add("second");

    original.Messages.ShouldBe(["first"]);
    original.MessageTotal.ShouldBe(1);
  }

  [Fact]
  public void AddPath_Over200_ShouldKeepTheFirstOnesAndTheTotal()
  {
    var log = ProcessingLog.Empty;

    for (var i = 0; i < ProcessingLog.MaxPaths + 50; i++)
    {
      log = log.AddPath($"src/File{i}.cs");
    }

    log.Paths.Count.ShouldBe(ProcessingLog.MaxPaths);
    log.Paths[0].ShouldBe("src/File0.cs");
    log.Paths[^1].ShouldBe($"src/File{ProcessingLog.MaxPaths - 1}.cs");
    log.PathTotal.ShouldBe(ProcessingLog.MaxPaths + 50);
    log.IsTruncated.ShouldBeTrue();
  }

  [Fact]
  public void AddPath_LongPathsOver64Kb_ShouldStopBeforeTheCountLimit()
  {
    var path = new string('a', 1024);
    var log = ProcessingLog.Empty;

    for (var i = 0; i < ProcessingLog.MaxPaths; i++)
    {
      log = log.AddPath(path);
    }

    log.Paths.Count.ShouldBeLessThan(ProcessingLog.MaxPaths);
    log.PathTotal.ShouldBe(ProcessingLog.MaxPaths);
  }

  [Fact]
  public void Restore_ShouldKeepTheItemsAndTotals()
  {
    var log = ProcessingLog.Restore(["message"], 3, ["path"], 5);

    log.Messages.ShouldBe(["message"]);
    log.MessageTotal.ShouldBe(3);
    log.Paths.ShouldBe(["path"]);
    log.PathTotal.ShouldBe(5);
    log.IsTruncated.ShouldBeTrue();
  }

  [Fact]
  public void Restore_ShouldContinueCountingTheStoredSize()
  {
    var full = new string('a', ProcessingLog.MaxBytes / 2);
    var log = ProcessingLog.Restore([full], 1, [], 0);

    var added = log.Add(full).Add("after");

    added.Messages.ShouldBe([full, "after"]);
    added.MessageTotal.ShouldBe(3);
  }

  [Fact]
  public void Restore_TotalBelowTheItemsKept_ShouldThrow()
  {
    Should.Throw<ArgumentException>(() => ProcessingLog.Restore(["a", "b"], 1, [], 0));
    Should.Throw<ArgumentException>(() => ProcessingLog.Restore([], 0, ["a", "b"], 1));
  }

  #endregion
}
