// ------------------------------------------------------------------------------------------------
// <copyright file="DiffTableRendererTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Application.Services.Comments;
using CodeCoverage.Domain.Coverage;

using static CodeCoverage.Application.Tests.Services.Comments.CommentSamples;

namespace CodeCoverage.Application.Tests.Services.Comments;

// The acceptance of M2.20: a line starts with + when the change is good and with - when it is bad.
public sealed class DiffTableRendererTests
{
  #region Methods

  [Theory]
  // title, base, head, expected prefix
  [InlineData("Hits", 10, 12, '+')]
  [InlineData("Hits", 12, 10, '-')]
  [InlineData("Misses", 10, 12, '-')]
  [InlineData("Misses", 12, 10, '+')]
  [InlineData("Partials", 10, 12, '-')]
  [InlineData("Partials", 12, 10, '+')]
  [InlineData("Hits", 10, 10, ' ')]
  [InlineData("Misses", 10, 10, ' ')]
  public void Render_ShouldPrefixByTheGoodOrTheBadDirection(string title, int before, int after, char prefix)
  {
    var lines = Render(Totals(title, before), Totals(title, after));

    lines.Single(line => line.Contains(title, StringComparison.Ordinal))[0].ShouldBe(prefix);
  }

  [Theory]
  [InlineData(100, 20, 100, 10, '+')]
  [InlineData(100, 10, 100, 20, '-')]
  [InlineData(100, 10, 100, 10, ' ')]
  public void Render_Coverage_ShouldBePrefixedByItsDirection(int baseHits, int baseMisses, int headHits, int headMisses, char prefix)
  {
    var lines = Render(new CoverageTotals(baseHits, baseMisses, 0, 0, 0), new CoverageTotals(headHits, headMisses, 0, 0, 0));

    lines.Single(static line => line.Contains("Coverage ", StringComparison.Ordinal) && !line.StartsWith("@@", StringComparison.Ordinal))[0].ShouldBe(prefix);
  }

  [Fact]
  public void Render_FilesLinesAndBranches_ShouldNeverBePrefixed()
  {
    var lines = DiffTableRenderer.Render("main", "#1", new CoverageTotals(10, 5, 0, 4, 0), new CoverageTotals(40, 1, 0, 9, 0), 3, 7, Config());

    var files = lines.Single(static line => line.Contains("Files", StringComparison.Ordinal));
    files.ShouldStartWith("  Files ");
    files.ShouldEndWith(" +4");
    lines.Single(static line => line.Contains("Lines", StringComparison.Ordinal)).ShouldStartWith("  Lines");
    lines.Single(static line => line.Contains("Branches", StringComparison.Ordinal)).ShouldStartWith("  Branches");
  }

  // As Codecov does: a count that is zero on both sides tells nothing.
  [Fact]
  public void Render_RowWithoutAnything_ShouldBeLeftOut()
  {
    var lines = Render(new CoverageTotals(10, 5, 0, 0, 0), new CoverageTotals(12, 5, 0, 0, 0));

    lines.ShouldNotContain(static line => line.Contains("Branches", StringComparison.Ordinal));
    lines.ShouldNotContain(static line => line.Contains("Partials", StringComparison.Ordinal));
    lines.ShouldContain(static line => line.Contains("Misses", StringComparison.Ordinal));
  }

  [Fact]
  public void Render_WholeGroupWithoutAnything_ShouldLeaveNoEmptyBlock()
  {
    var lines = DiffTableRenderer.Render("main", "#1", default(CoverageTotals), default, 0, 0, Config());

    lines.Count(static line => line.StartsWith('=')).ShouldBe(1);
    lines[^1].ShouldBe("```");
    lines[^2].ShouldContain("Coverage");
  }

  [Fact]
  public void Render_LongBranchName_ShouldKeepEveryLineAligned()
  {
    var lines = DiffTableRenderer.Render("release/2026-11-a-very-long-branch-name", "#1234", BaseTotals, HeadTotals, 210, 212, Config());
    var width = lines[2].Length;

    lines[1].Length.ShouldBe(width);
    lines[1].ShouldStartWith("@@");
    lines[1].ShouldEndWith("@@");
    lines.Where(static line => line.StartsWith('=')).ShouldAllBe(line => line.Length == width);
    lines[2].ShouldStartWith("##");
    lines[2].ShouldEndWith("##");
    lines[2].ShouldContain(" release/2026-11-a-very-long-branch-name ");
    lines[2].ShouldContain(" #1234 ");
  }

  [Fact]
  public void Render_ShouldFenceADiffBlockAndEndNoLineWithASpace()
  {
    var lines = Render(BaseTotals, HeadTotals);

    lines[0].ShouldBe("```diff");
    lines[^1].ShouldBe("```");
    lines.ShouldAllBe(static line => !line.EndsWith(' '));
  }

  // A fall smaller than the precision still marks the line as bad, as the rounding of Codecov does (coverage.round is
  // "down" by default).
  [Fact]
  public void Render_ChangeSmallerThanThePrecision_ShouldStillBeSigned()
  {
    var lines = Render(new CoverageTotals(1_000_000, 1, 0, 0, 0), new CoverageTotals(1_000_000, 2, 0, 0, 0));

    lines.Single(static line => line.Contains(" Coverage", StringComparison.Ordinal) && !line.StartsWith("@@", StringComparison.Ordinal))
         .ShouldBe("- Coverage    99.99%    99.99%   -0.01%");
  }

  [Fact]
  public void Render_NullArguments_ShouldThrow()
  {
    Should.Throw<ArgumentNullException>(() => DiffTableRenderer.Render(null!, "#1", null, HeadTotals, null, 1, Config()));
    Should.Throw<ArgumentNullException>(() => DiffTableRenderer.Render("main", null!, null, HeadTotals, null, 1, Config()));
    Should.Throw<ArgumentNullException>(() => DiffTableRenderer.Render("main", "#1", null, HeadTotals, null, 1, null!));
  }

  private static IReadOnlyList<string> Render(CoverageTotals before, CoverageTotals after) =>
    DiffTableRenderer.Render("main", "#1", before, after, 1, 1, Config());

  private static CoverageTotals Totals(string title, int value) =>
    title switch
    {
      "Hits" => new CoverageTotals(value, 5, 5, 0, 0),
      "Misses" => new CoverageTotals(50, value, 5, 0, 0),
      var _ => new CoverageTotals(50, 5, value, 0, 0),
    };

  #endregion
}
