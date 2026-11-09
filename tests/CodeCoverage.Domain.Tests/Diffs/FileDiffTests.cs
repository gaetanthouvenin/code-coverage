// ------------------------------------------------------------------------------------------------
// <copyright file="FileDiffTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Diffs;

namespace CodeCoverage.Domain.Tests.Diffs;

public sealed class FileDiffTests
{
  #region Methods

  [Fact]
  public void Create_WithPatch_ShouldGatherTheLinesOfEveryHunk()
  {
    var diff = FileDiff.Create(
      "b.cs",
      "a.cs",
      FileDiffStatus.Renamed,
      "@@ -1 +1 @@\n-a\n+b\n@@ -9,2 +9,3 @@\n c\n+d\n e");

    diff.Path.ShouldBe("b.cs");
    diff.PreviousPath.ShouldBe("a.cs");
    diff.Status.ShouldBe(FileDiffStatus.Renamed);
    diff.HasPatch.ShouldBeTrue();
    diff.Hunks.Count.ShouldBe(2);
    diff.RemovedLines.ShouldBe([1]);
    diff.AddedLines.ShouldBe([1, 10]);
  }

  [Fact]
  public void Create_NullPatch_ShouldBeUnavailable()
  {
    var diff = FileDiff.Create("a.cs", null, FileDiffStatus.Modified, null);

    diff.HasPatch.ShouldBeFalse();
    diff.Hunks.ShouldBeEmpty();
  }

  [Fact]
  public void Create_EmptyPatch_ShouldBeAvailableWithoutChange()
  {
    var diff = FileDiff.Create("a.cs", null, FileDiffStatus.Modified, string.Empty);

    diff.HasPatch.ShouldBeTrue();
    diff.Hunks.ShouldBeEmpty();
  }

  [Fact]
  public void Create_UnreadablePatch_ShouldBeUnavailable()
  {
    var diff = FileDiff.Create("a.cs", null, FileDiffStatus.Modified, "@@ -1,5 +1,5 @@\n a");

    diff.HasPatch.ShouldBeFalse();
    diff.Patch.ShouldBeNull();
    diff.Hunks.ShouldBeEmpty();
  }

  [Theory]
  [InlineData("")]
  [InlineData("  ")]
  public void Create_BlankPath_ShouldThrow(string path)
  {
    Should.Throw<ArgumentException>(() => FileDiff.Create(path, null, FileDiffStatus.Added, null));
  }

  #endregion
}
