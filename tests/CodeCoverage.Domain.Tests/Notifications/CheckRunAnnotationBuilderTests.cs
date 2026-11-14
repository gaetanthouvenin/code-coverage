// ------------------------------------------------------------------------------------------------
// <copyright file="CheckRunAnnotationBuilderTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Coverage;
using CodeCoverage.Domain.Diffs;
using CodeCoverage.Domain.Notifications;
using CodeCoverage.Domain.Paths;

namespace CodeCoverage.Domain.Tests.Notifications;

public sealed class CheckRunAnnotationBuilderTests
{
  #region Fields

  private static readonly PathPatternSet AllPaths = new([]);

  // Lines 10 to 16 added.
  private const string Patch = "@@ -9,0 +10,7 @@\n+a\n+b\n+c\n+d\n+e\n+f\n+g";

  #endregion

  #region Methods

  [Fact]
  public void Build_ShouldGroupConsecutiveUncoveredLines()
  {
    var head = View(
      FileCoverage.Create(
        "src/a.cs",
        [
          Line(10, 0), Line(11, 0), Line(12, 0),
          Line(13, 4),
          Line(14, 0),
          // Partial: executed, one branch of two.
          new LineCoverage(15, 2, 1, 2, false, null),
          new LineCoverage(16, 2, 1, 2, false, null),
          // Not in the patch: never annotated.
          Line(30, 0),
        ]
      )
    );

    var annotations = CheckRunAnnotationBuilder.Build(head, Diff(FileDiff.Create("src/a.cs", null, FileDiffStatus.Modified, Patch)));

    annotations.ShouldBe(
      [
        new CheckRunAnnotation("src/a.cs", 10, 12, "Added lines #L10 - L12 were not covered by tests"),
        new CheckRunAnnotation("src/a.cs", 14, 14, "Added line #L14 was not covered by tests"),
        new CheckRunAnnotation("src/a.cs", 15, 16, "Added lines #L15 - L16 were partially covered by tests"),
      ]
    );
  }

  // The classification of the patch status (CoverageMath.Classify): branches decide a branch line, not its hits.
  [Fact]
  public void Build_BranchLines_ShouldFollowThePatchStatus()
  {
    var head = View(
      FileCoverage.Create(
        "src/a.cs",
        [
          // Every branch covered, no hit recorded: a hit, no note.
          new LineCoverage(10, 0, 2, 2, false, null),
          // Executed, no branch covered: a miss.
          new LineCoverage(11, 3, 0, 2, false, null),
          // One branch of two: partial.
          new LineCoverage(12, 0, 1, 2, false, null),
        ]
      )
    );

    var annotations = CheckRunAnnotationBuilder.Build(head, Diff(FileDiff.Create("src/a.cs", null, FileDiffStatus.Modified, Patch)));

    annotations.ShouldBe(
      [
        new CheckRunAnnotation("src/a.cs", 11, 11, "Added line #L11 was not covered by tests"),
        new CheckRunAnnotation("src/a.cs", 12, 12, "Added line #L12 was partially covered by tests"),
      ]
    );
  }

  // A line the report does not cover splits a group: the lines of a group are consecutive in the file.
  [Fact]
  public void Build_GapInTheReport_ShouldSplitTheGroup()
  {
    var head = View(FileCoverage.Create("src/a.cs", [Line(10, 0), Line(12, 0)]));

    var annotations = CheckRunAnnotationBuilder.Build(head, Diff(FileDiff.Create("src/a.cs", null, FileDiffStatus.Added, Patch)));

    annotations.Select(static a => (a.StartLine, a.EndLine)).ShouldBe([(10, 10), (12, 12)]);
  }

  [Fact]
  public void Build_FileWithoutPatchOrRemovedOrUnknown_ShouldGiveNothing()
  {
    var head = View(FileCoverage.Create("src/a.cs", [Line(10, 0)]), FileCoverage.Create("src/b.cs", [Line(10, 0)]));

    var annotations = CheckRunAnnotationBuilder.Build(
      head,
      Diff(
        FileDiff.Create("src/a.cs", null, FileDiffStatus.Modified, null),
        FileDiff.Create("src/b.cs", null, FileDiffStatus.Removed, Patch),
        FileDiff.Create("src/unknown.cs", null, FileDiffStatus.Added, Patch)
      )
    );

    annotations.ShouldBeEmpty();
  }

  [Fact]
  public void Batch_ShouldSplitIntoBatchesOf50()
  {
    var annotations = Enumerable.Range(1, 120).Select(static i => new CheckRunAnnotation("src/a.cs", i, i, "m")).ToList();

    var batches = CheckRunAnnotationBuilder.Batch(annotations);

    batches.Select(static b => b.Count).ShouldBe([50, 50, 20]);
    batches.SelectMany(static b => b).ShouldBe(annotations);
    CheckRunAnnotationBuilder.Batch([]).ShouldBeEmpty();
  }

  [Fact]
  public void Build_NullArguments_ShouldThrow()
  {
    Should.Throw<ArgumentNullException>(() => CheckRunAnnotationBuilder.Build(null!, Diff()));
    Should.Throw<ArgumentNullException>(() => CheckRunAnnotationBuilder.Build(View(), null!));
    Should.Throw<ArgumentNullException>(() => CheckRunAnnotationBuilder.Batch(null!));
  }

  private static LineCoverage Line(int line, long hits) => new(line, hits, 0, 0, false, null);

  private static ReportView View(params FileCoverage[] files) => ReportView.Build([new SessionReport([], CoverageReport.Create(files))], [], AllPaths);

  private static DiffSet Diff(params FileDiff[] files) => new(files, Truncated: false, DiffSource.PullFiles);

  #endregion
}
