// ------------------------------------------------------------------------------------------------
// <copyright file="GitHubPatchFixtureTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Text;
using CodeCoverage.Domain.Coverage;
using CodeCoverage.Domain.Diffs;

namespace CodeCoverage.Domain.Tests.Diffs;

// Real GitHub patches, checked against the two versions of their file: see the README of the fixtures.
public sealed class GitHubPatchFixtureTests
{
  #region Fields

  private static readonly string PatchesDirectory =
    Path.Combine(AppContext.BaseDirectory, "fixtures", "github", "patches");

  #endregion

  #region Properties

  public static TheoryData<string> Fixtures =>
    [.. Directory.GetFiles(PatchesDirectory, "*.diff").Select(Path.GetFileNameWithoutExtension).OfType<string>()];

  #endregion

  #region Methods

  [Theory]
  [MemberData(nameof(Fixtures))]
  public void Parse_RealPatch_ShouldPointAtTheChangedLines(string name)
  {
    var patch = ReadText($"{name}.diff");
    var diff = Create(patch);

    diff.RemovedLines.Select(line => ReadLines($"{name}.before")[line - 1]).ShouldBe(BodyLines(patch, '-'));
    diff.AddedLines.Select(line => ReadLines($"{name}.after")[line - 1]).ShouldBe(BodyLines(patch, '+'));
  }

  [Theory]
  [MemberData(nameof(Fixtures))]
  public void Shift_RealPatch_ShouldKeepEveryLineOnItsContent(string name)
  {
    var before = ReadLines($"{name}.before");
    var after = ReadLines($"{name}.after");
    var diff = Create(ReadText($"{name}.diff"));
    var lines = Enumerable.Range(1, before.Length).Select(line => new LineCoverage(line, line, 0, 0, false, null));

    var shifted = LineShifter.Shift(diff, lines);

    shifted.Count.ShouldBe(before.Length - diff.RemovedLines.Count);
    shifted.Count.ShouldBe(after.Length - diff.AddedLines.Count);
    shifted.ShouldAllBe(line => after[line.Line - 1] == before[(int)line.Hits - 1]);
  }

  [Fact]
  public void Parse_RealRemovedLuaComment_ShouldCountTheLine()
  {
    var diff = Create(ReadText("removed-lua-comments.diff"));

    ReadLines("removed-lua-comments.before")[4].ShouldBe("-- TODO(conni2461): also table of contents for tree-sitter-lua");
    diff.RemovedLines.ShouldContain(5);
  }

  private static FileDiff Create(string patch)
  {
    var diff = FileDiff.Create("file", null, FileDiffStatus.Modified, patch);

    diff.HasPatch.ShouldBeTrue();

    return diff;
  }

  private static IEnumerable<string> BodyLines(string patch, char prefix)
  {
    return patch.Split('\n').Where(line => line.StartsWith(prefix)).Select(line => line[1..]);
  }

  private static string ReadText(string file)
  {
    var path = Path.Combine(PatchesDirectory, file);

    return File.Exists(path) ? Encoding.UTF8.GetString(File.ReadAllBytes(path)) : string.Empty;
  }

  private static string[] ReadLines(string file)
  {
    var lines = ReadText(file).Split('\n');

    return lines[^1].Length == 0 ? lines[..^1] : lines;
  }

  #endregion
}
