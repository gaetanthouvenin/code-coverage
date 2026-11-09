// ------------------------------------------------------------------------------------------------
// <copyright file="CarryforwardBuilderTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Commits;
using CodeCoverage.Domain.Contracts.Forges;
using CodeCoverage.Domain.Coverage;
using CodeCoverage.Domain.Diffs;
using CodeCoverage.Domain.Git;
using CodeCoverage.Domain.Paths;

namespace CodeCoverage.Domain.Tests.Commits;

public sealed class CarryforwardBuilderTests
{
  #region Fields

  private const string SourcePath = "src/A.cs";

  private const long SessionId = 12;

  private const long UploadId = 34;

  private static readonly GitSha AncestorSha = GitSha.Parse(new string('a', GitSha.Length));

  private static readonly GitSha OriginSha = GitSha.Parse(new string('b', GitSha.Length));

  private static readonly GitSha CommitSha = GitSha.Parse(new string('c', GitSha.Length));

  private static readonly GitSha BlobOne = GitSha.Parse(new string('1', GitSha.Length));

  private static readonly GitSha BlobTwo = GitSha.Parse(new string('2', GitSha.Length));

  private static readonly PathPatternSet AllPaths = new([]);

  private static readonly DiffSet EmptyDiff = new([], false, DiffSource.Compare);

  // Line 2 of the old file is replaced by two lines: what follows moves by one.
  private const string Patch = "@@ -1,3 +1,4 @@\n a\n-b\n+x\n+y\n c";

  #endregion

  #region Methods

  [Fact]
  public void Plan_FlagUploaded_ShouldNotCarry()
  {
    var plan = CarryforwardBuilder.Plan(["back", "front"], ["front"]);

    plan.Missing.ShouldBe(["back"]);
  }

  [Fact]
  public void Plan_EveryFlagUploaded_ShouldBeEmpty()
  {
    CarryforwardBuilder.Plan(["back"], ["back", "other"]).IsEmpty.ShouldBeTrue();
  }

  [Fact]
  public void Build_SessionWithMixedFlags_ShouldSkipWhenAnyUploaded()
  {
    var plan = CarryforwardBuilder.Plan(["back", "front"], ["front"]);

    var carried = CarryforwardBuilder.Build(plan, Source(["back", "front"]), EmptyDiff, null, null, AllPaths);

    carried.ShouldBeNull();
  }

  [Fact]
  public void Build_SessionWithoutMissingFlag_ShouldSkip()
  {
    CarryforwardBuilder.Build(Back(), Source(["other"]), EmptyDiff, null, null, AllPaths).ShouldBeNull();
  }

  [Fact]
  public void Build_ModifiedFile_ShouldShiftLines()
  {
    var diff = Diff(FileDiff.Create(SourcePath, null, FileDiffStatus.Modified, Patch));

    var carried = Carry(diff);

    carried.Report.Files.ShouldHaveSingleItem().Lines.Select(line => line.Line).ShouldBe([1, 4]);
    carried.DataUploadId.ShouldBeNull();
  }

  [Fact]
  public void Build_RenamedFile_ShouldMovePath()
  {
    var diff = Diff(FileDiff.Create("src/B.cs", SourcePath, FileDiffStatus.Renamed, Patch));

    var carried = Carry(diff);

    carried.Report.Files.ShouldHaveSingleItem().Path.ShouldBe("src/B.cs");
    carried.Report.Files[0].Lines.Select(line => line.Line).ShouldBe([1, 4]);
    carried.DataUploadId.ShouldBeNull();
  }

  [Fact]
  public void Build_RenamedWithoutPatchAndSameBlob_ShouldMovePath()
  {
    var diff = Diff(FileDiff.Create("src/B.cs", SourcePath, FileDiffStatus.Renamed, null));

    var carried = Carry(diff, Tree(AncestorSha, (SourcePath, BlobOne)), Tree(CommitSha, ("src/B.cs", BlobOne)));

    carried.Report.Files.ShouldHaveSingleItem().Path.ShouldBe("src/B.cs");
    carried.Report.Files[0].Lines.Select(line => line.Line).ShouldBe([1, 2, 3]);
    carried.DroppedFiles.ShouldBeEmpty();
  }

  [Fact]
  public void Build_RemovedFile_ShouldDropWithoutReporting()
  {
    var carried = Carry(Diff(FileDiff.Create(SourcePath, null, FileDiffStatus.Removed, null)));

    carried.Report.Files.ShouldBeEmpty();
    carried.DroppedFiles.ShouldBeEmpty();
    carried.DataUploadId.ShouldBeNull();
  }

  [Fact]
  public void Build_NoDiffAndDifferentBlob_ShouldDropFile()
  {
    var carried = CarryNoDiff(Tree(AncestorSha, (SourcePath, BlobOne)), Tree(CommitSha, (SourcePath, BlobTwo)));

    carried.Report.Files.ShouldBeEmpty();
    carried.DroppedFiles.ShouldBe([SourcePath]);
  }

  [Fact]
  public void Build_NoDiffAndSameBlob_ShouldKeepFile()
  {
    var carried = CarryNoDiff(Tree(AncestorSha, (SourcePath, BlobOne)), Tree(CommitSha, (SourcePath, BlobOne)));

    carried.Report.Files.ShouldHaveSingleItem().Path.ShouldBe(SourcePath);
    carried.DroppedFiles.ShouldBeEmpty();
  }

  [Theory]
  [InlineData(false)]
  [InlineData(true)]
  public void Build_NoDiffAndUnknownBlob_ShouldDropFile(bool ancestorKnown)
  {
    var known = Tree(AncestorSha, (SourcePath, BlobOne));
    var unknown = Tree(CommitSha);

    var carried = CarryNoDiff(ancestorKnown ? known : unknown, ancestorKnown ? unknown : known);

    carried.DroppedFiles.ShouldBe([SourcePath]);
  }

  [Fact]
  public void Build_TruncatedDiffNotListingTheFile_ShouldCompareBlobs()
  {
    var diff = new DiffSet([], true, DiffSource.Compare);

    var carried = Carry(diff, Tree(AncestorSha, (SourcePath, BlobOne)), Tree(CommitSha, (SourcePath, BlobTwo)));

    carried.DroppedFiles.ShouldBe([SourcePath]);
  }

  [Fact]
  public void Build_ModifiedWithoutPatchAndDifferentBlob_ShouldDropFile()
  {
    var diff = Diff(FileDiff.Create(SourcePath, null, FileDiffStatus.Modified, null));

    var carried = Carry(diff, Tree(AncestorSha, (SourcePath, BlobOne)), Tree(CommitSha, (SourcePath, BlobTwo)));

    carried.DroppedFiles.ShouldBe([SourcePath]);
  }

  [Fact]
  public void Build_Unshifted_ShouldReferenceSourceBlob()
  {
    var carried = Carry(EmptyDiff);

    carried.DataUploadId.ShouldBe(UploadId);
    carried.Report.Files.ShouldHaveSingleItem().Lines.Select(line => line.Line).ShouldBe([1, 2, 3]);
  }

  [Fact]
  public void Build_PatchThatMovesNoCoveredLine_ShouldReferenceSourceBlob()
  {
    var diff = Diff(FileDiff.Create(SourcePath, null, FileDiffStatus.Modified, "@@ -9,1 +9,2 @@\n z\n+new"));

    Carry(diff).DataUploadId.ShouldBe(UploadId);
  }

  [Fact]
  public void Build_SourceWithItsOwnBlob_ShouldNeedAnotherOne()
  {
    var source = Source(["back"]) with { DataUploadId = null };

    CarryforwardBuilder.Build(Back(), source, EmptyDiff, null, null, AllPaths).ShouldNotBeNull().DataUploadId.ShouldBeNull();
  }

  [Fact]
  public void Build_OutsideFlagPaths_ShouldFilterWithoutReporting()
  {
    var paths = new PathPatternSet([PathPattern.Parse("lib/")]);

    var carried = CarryforwardBuilder.Build(Back(), Source(["back"]), EmptyDiff, null, null, paths).ShouldNotBeNull();

    carried.Report.Files.ShouldBeEmpty();
    carried.DroppedFiles.ShouldBeEmpty();
    carried.DataUploadId.ShouldBeNull();
  }

  [Fact]
  public void Build_FirstCopy_ShouldNameTheAncestorAsOrigin()
  {
    Carry(EmptyDiff).CarriedForwardFromSha.ShouldBe(AncestorSha);
  }

  [Fact]
  public void Build_CopyOfACopy_ShouldKeepTheFirstOrigin()
  {
    var source = Source(["back"]) with { CarriedForwardFromSha = OriginSha };

    CarryforwardBuilder.Build(Back(), source, EmptyDiff, null, null, AllPaths).ShouldNotBeNull().CarriedForwardFromSha.ShouldBe(OriginSha);
  }

  [Theory]
  [InlineData(null, "Carriedforward")]
  [InlineData("", "Carriedforward")]
  [InlineData("backend", "CF[1] - backend")]
  [InlineData("CF[1] - backend", "CF[2] - backend")]
  [InlineData("CF[9] - CF[1] - x", "CF[10] - CF[1] - x")]
  [InlineData("CF[x] - backend", "CF[1] - CF[x] - backend")]
  public void Build_ShouldNameTheCopyLikeCodecov(string? name, string expected)
  {
    var source = Source(["back"]) with { Name = name };

    CarryforwardBuilder.Build(Back(), source, EmptyDiff, null, null, AllPaths).ShouldNotBeNull().Name.ShouldBe(expected);
  }

  private static CarryforwardPlan Back() => CarryforwardBuilder.Plan(["back"], []);

  private static CarriedSession Carry(DiffSet diff, ForgeTree? ancestor = null, ForgeTree? commit = null) =>
    CarryforwardBuilder.Build(Back(), Source(["back"]), diff, ancestor, commit, AllPaths).ShouldNotBeNull();

  private static CarriedSession CarryNoDiff(ForgeTree ancestor, ForgeTree commit) =>
    CarryforwardBuilder.Build(Back(), Source(["back"]), null, ancestor, commit, AllPaths).ShouldNotBeNull();

  private static DiffSet Diff(params FileDiff[] files) => new(files, false, DiffSource.Compare);

  private static ForgeTree Tree(GitSha sha, params (string Path, GitSha Blob)[] files) =>
    new(sha, [.. files.Select(file => new ForgeTreeEntry(file.Path, file.Blob, false))], false);

  private static CarryforwardSource Source(string[] flags) =>
    new(
      SessionId,
      AncestorSha,
      null,
      UploadId,
      flags,
      "backend",
      CoverageReport.Create(
        [FileCoverage.Create(SourcePath, Enumerable.Range(1, 3).Select(static line => new LineCoverage(line, 1, 0, 0, false, null)))]
      )
    );

  #endregion
}
