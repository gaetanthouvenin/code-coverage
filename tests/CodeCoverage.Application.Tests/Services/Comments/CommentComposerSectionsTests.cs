// ------------------------------------------------------------------------------------------------
// <copyright file="CommentComposerSectionsTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Application.Services;
using CodeCoverage.Application.Services.Comments;
using CodeCoverage.Domain.Comparisons;
using CodeCoverage.Domain.Config;
using CodeCoverage.Domain.Coverage;
using CodeCoverage.Domain.Diffs;

using static CodeCoverage.Application.Tests.Services.Comments.CommentSamples;

namespace CodeCoverage.Application.Tests.Services.Comments;

// The flags, the files and the footer of the comment (ARCHITECTURE §8.4), and its length.
public sealed class CommentComposerSectionsTests
{
  #region Fields

  private static readonly FlagComparison Backend = Flag("backend", new CoverageTotals(847, 153, 0, 0, 0), new CoverageTotals(851, 149, 0, 0, 0), new CoverageTotals(8, 2, 0, 0, 0));

  private static readonly FlagComparison Frontend = Flag("frontend", new CoverageTotals(7002, 2998, 0, 0, 0), new CoverageTotals(7002, 2998, 0, 0, 0), carriedForward: true);

  private static readonly ImpactedFile Foo = File("src/Api/Foo.cs", new CoverageTotals(75, 25, 0, 0, 0), new CoverageTotals(72, 28, 0, 0, 0), new CoverageTotals(6, 4, 0, 0, 0));

  private static readonly ImpactedFile Bar = File("src/Api/Bar.cs", null, new CoverageTotals(9, 1, 0, 0, 0), new CoverageTotals(9, 1, 0, 0, 0), FileDiffStatus.Added);

  private static readonly ImpactedFile Old = File("src/Old.cs", new CoverageTotals(5, 5, 0, 0, 0), null, default, FileDiffStatus.Removed);

  private static readonly IndirectChange[] Indirect =
  [
    new("src/Shared/A.cs", IndirectChangeKind.Changed, -1, 1, 0, -1m),
    new("src/Shared/B.cs", IndirectChangeKind.Changed, 2, -2, 0, 2m),
  ];

  #endregion

  #region Methods

  // The whole comment, with its default layout: header, diff, flags, files, footer.
  [Fact]
  public Task Compose_DefaultLayout_ShouldMatchSnapshot() =>
    Verify(
      CommentComposer.Compose(
        Content(
          Config(("show_carryforward_flags", ConfigNode.FromBoolean(true))),
          Comparison(files: [Foo, Bar, Old], flags: [Frontend, Backend], indirect: Indirect),
          sessions: [Uploaded(1, "backend"), Uploaded(2, "backend"), CarriedForward(3, OlderSha, "frontend")],
          failedUploads: 1
        )
      )
    );

  // show_carryforward_flags is false by default: a flag whose every session was carried forward is not listed.
  [Fact]
  public Task Compose_CarryforwardHidden_ShouldOmitFlag() =>
    Verify(
      CommentComposer.Compose(
        Content(
          Layout("header, flags"),
          Comparison(flags: [Frontend, Backend]),
          sessions: [Uploaded(1, "backend"), CarriedForward(2, OlderSha, "frontend")]
        )
      )
    );

  [Fact]
  public Task Compose_FilesLimit_ShouldAddMore() =>
    Verify(
      CommentComposer.Compose(
        Content(
          Layout("header, files:2"),
          Comparison(files: [.. Enumerable.Range(1, 5).Select(static i => File($"src/File{i}.cs", new CoverageTotals(50, 50, 0, 0, 0), new CoverageTotals(50, 50, 0, 0, 0), new CoverageTotals(0, i, 0, 0, 0)))])
        )
      )
    );

  [Fact]
  public Task Compose_DeletedFile_ShouldStrike() =>
    Verify(CommentComposer.Compose(Content(Layout("header, files"), Comparison(files: [Old, Foo]))));

  [Fact]
  public Task Compose_LayoutOrder_ShouldFollowConfig() =>
    Verify(
      CommentComposer.Compose(
        Content(
          Layout("footer, files, flags, diff"),
          Comparison(files: [Foo], flags: [Backend]),
          sessions: [Uploaded(1, "backend")]
        )
      )
    );

  // The acceptance of M2.21: by missing patch lines, then by the size of the change, whatever its direction.
  [Fact]
  public void Compose_Files_ShouldSortByMissingPatchLinesThenByAbsoluteChange()
  {
    var hundred = new CoverageTotals(100, 100, 0, 0, 0);
    ImpactedFile[] files =
    [
      File("a-few-missing.cs", hundred, hundred, new CoverageTotals(0, 1, 0, 0, 0)),
      File("b-small-rise.cs", hundred, new CoverageTotals(102, 98, 0, 0, 0), new CoverageTotals(0, 3, 1, 0, 0)),
      File("c-large-fall.cs", hundred, new CoverageTotals(80, 120, 0, 0, 0), new CoverageTotals(0, 4, 0, 0, 0)),
      File("d-most-missing.cs", hundred, hundred, new CoverageTotals(0, 9, 0, 0, 0)),
      File("e-same-as-b.cs", hundred, new CoverageTotals(102, 98, 0, 0, 0), new CoverageTotals(0, 4, 0, 0, 0)),
      File("f-covered.cs", hundred, hundred, new CoverageTotals(5, 0, 0, 0, 0)),
    ];

    var body = CommentComposer.Compose(Content(Layout("header, files"), Comparison(files: files), links: Links(null)));

    string[] listed = [.. body.Split('\n').Where(static line => line.StartsWith("| ", StringComparison.Ordinal) && line.Contains(".cs", StringComparison.Ordinal)).Select(static line => line.Split(' ')[1])];

    // Partials miss coverage as misses do; the fully covered file is not listed.
    listed.ShouldBe(["d-most-missing.cs", "c-large-fall.cs", "b-small-rise.cs", "e-same-as-b.cs", "a-few-missing.cs"]);
  }

  [Fact]
  public void Compose_Over60000_ShouldTruncateFilesFirst()
  {
    ImpactedFile[] files =
    [
      .. Enumerable.Range(1, 1500)
                   .Select(static i => File($"src/{new string('d', 60)}/File{i:D4}.cs", new CoverageTotals(50, 50, 0, 0, 0), new CoverageTotals(50, 50, 0, 0, 0), new CoverageTotals(0, 2000 - i, 0, 0, 0))),
    ];
    var content = Content(
      Config(("layout", ConfigNode.FromText("header, diff, flags, files:1500, footer"))),
      Comparison(files: files, flags: [Backend], indirect: Indirect),
      sessions: [Uploaded(1, "backend")]
    );

    var body = CommentComposer.Compose(content);
    var rows = body.Split('\n').Count(static line => line.Contains("/File", StringComparison.Ordinal));
    var row = body.Split('\n').First(static line => line.Contains("/File", StringComparison.Ordinal)).Length + 1;

    body.Length.ShouldBeLessThanOrEqualTo(CommentTruncator.MaxLength);
    rows.ShouldBeInRange(1, 1499);
    // D. As many rows as fit: one more would not, so the room left is shorter than a row and its count.
    (CommentTruncator.MaxLength - body.Length).ShouldBeLessThan(row + 10);
    // The most important files stay, and those left out are counted.
    body.ShouldContain("File0001.cs");
    body.ShouldNotContain("File1500.cs");
    body.ShouldContain($"| ... and [{1500 - rows} more](https://coverage.example.com/github/octo/repo/pull/123) | | |");
    // Nothing else is given up, and the body says it was shortened.
    body.ShouldContain("```diff");
    body.ShouldContain("| backend |");
    body.ShouldContain("| ... and [2 files with indirect coverage changes]");
    body.ShouldContain("</details>");
    body.ShouldEndWith("</sub>");
    body.ShouldContain("> :warning: This comment is too long to show every file: see [the full report](https://coverage.example.com/github/octo/repo/pull/123).");

    // One more row would not fit.
    var oneMore = 60 + "| [src//File0001.cs]() | `50.00% <0.00%> (ø)` | |".Length;
    (body.Length + oneMore).ShouldBeGreaterThan(CommentTruncator.MaxLength - 400);
  }

  [Fact]
  public void Compose_UnderTheCeiling_ShouldNotSayItWasShortened()
  {
    var body = CommentComposer.Compose(Content(Layout("header, files:1"), Comparison(files: [Foo, Bar])));

    body.ShouldContain("| ... and [1 more]");
    body.ShouldNotContain("too long");
  }

  [Fact]
  public void Compose_FilesLimitOfZero_ShouldOnlyCountTheFiles()
  {
    var body = CommentComposer.Compose(Content(Layout("header, files:0"), Comparison(files: [Foo, Bar])));

    body.ShouldContain("Files with missing lines");
    body.ShouldNotContain("Foo.cs");
    body.ShouldContain("| ... and [2 more]");
  }

  [Fact]
  public void Compose_NoFileMissingCoverage_ShouldWriteNoTable()
  {
    var covered = File("src/Covered.cs", null, new CoverageTotals(5, 0, 0, 0, 0), new CoverageTotals(5, 0, 0, 0, 0));

    var body = CommentComposer.Compose(Content(Layout("header, files"), Comparison(files: [covered])));

    body.ShouldNotContain("Files with missing lines");
    body.ShouldNotContain("<details>");
  }

  [Theory]
  [InlineData(1, "[see 1 file with indirect coverage changes](https://coverage.example.com/github/octo/repo/pull/123)")]
  [InlineData(2, "[see 2 files with indirect coverage changes](https://coverage.example.com/github/octo/repo/pull/123)")]
  public void Compose_OnlyIndirectChanges_ShouldPointToThem(int count, string expected)
  {
    var body = CommentComposer.Compose(Content(Layout("header, files"), Comparison(indirect: [.. Indirect.Take(count)])));

    body.ShouldContain(expected);
    body.ShouldNotContain("Files with missing lines");
  }

  [Fact]
  public void Compose_PathWithMarkdown_ShouldBeEscapedAndItsLinkEncoded()
  {
    var odd = File("docs/a|b/my_file [1].cs", null, new CoverageTotals(1, 1, 0, 0, 0), new CoverageTotals(1, 1, 0, 0, 0));

    var body = CommentComposer.Compose(Content(Layout("header, files"), Comparison(files: [odd])));

    body.ShouldContain("| [docs/a\\|b/my\\_file \\[1\\].cs](https://coverage.example.com/github/octo/repo/commit/" + HeadSha.Value + "/blob/docs/a%7Cb/my_file%20%5B1%5D.cs?pull=123) |");
  }

  [Fact]
  public void Compose_FlagTheHeadNoLongerHas_ShouldShowAQuestionMark()
  {
    var gone = Flag("legacy", new CoverageTotals(1, 1, 0, 0, 0), null);

    var body = CommentComposer.Compose(Content(Layout("header, flags"), Comparison(flags: [gone, Backend])));

    body.ShouldContain("| legacy | `?` | |");
  }

  // S1. make_metrics of Codecov receives None for a flag the base does not hold, and writes "?" for its change.
  [Fact]
  public void Compose_FlagNewInTheHead_ShouldHaveNoChangeToShow()
  {
    var fresh = Flag("fresh", null, new CoverageTotals(3, 1, 0, 0, 0), new CoverageTotals(3, 1, 0, 0, 0));

    var body = CommentComposer.Compose(Content(Layout("header, flags"), Comparison(flags: [fresh])));

    body.ShouldContain("| fresh | `75.00% <75.00%> (?)` | |");
  }

  // S1. make_metrics of Codecov receives False for a file the base does not hold ("or False"), and writes "ø".
  [Fact]
  public void Compose_NewFile_ShouldShowNoImpactAsItsChange()
  {
    var created = File("src/New.cs", null, new CoverageTotals(3, 1, 0, 0, 0), new CoverageTotals(3, 1, 0, 0, 0));

    var body = CommentComposer.Compose(Content(Layout("header, files"), Comparison(files: [created])));

    body.ShouldContain("`75.00% <75.00%> (\u00f8)`");
  }

  // S1. The empty set only for a base that is there, with a coverage that cannot be computed.
  [Fact]
  public void Compose_BaseWithoutCoverableLine_ShouldShowTheEmptySetAsItsChange()
  {
    var file = File("src/Empty.cs", new CoverageTotals(0, 0, 0, 0, 0), new CoverageTotals(3, 1, 0, 0, 0), new CoverageTotals(3, 1, 0, 0, 0));
    var flag = Flag("empty", new CoverageTotals(0, 0, 0, 0, 0), new CoverageTotals(3, 1, 0, 0, 0), new CoverageTotals(3, 1, 0, 0, 0));

    var body = CommentComposer.Compose(Content(Layout("header, flags, files"), Comparison(files: [file], flags: [flag])));

    body.ShouldContain("| empty | `75.00% <75.00%> (\u2205)` | |");
    body.Split('\n').Single(static line => line.Contains("src/Empty.cs", StringComparison.Ordinal)).ShouldContain("(\u2205)");
  }

  // F23. The names Codecov gives the table of the files with missing lines, and its short footer.
  [Fact]
  public void Compose_CondensedFilesAndFooter_ShouldWriteTheFilesTableAndTheFooter()
  {
    var content = Content(Layout("condensed_header, condensed_files, condensed_footer"), Comparison(files: [Old, Foo]));

    var body = CommentComposer.Compose(content);

    body.ShouldContain("Files with missing lines");
    body.ShouldContain("<sub>");
  }

  // D. The numbers never follow the culture of the host: a comma decimal separator changes nothing.
  [Fact]
  public void Compose_UnderACommaDecimalCulture_ShouldWriteTheSameBody()
  {
    var content = Content(Layout("header, diff, flags, files, footer"), Comparison(files: [Old, Foo], flags: [Backend], indirect: Indirect), sessions: [Uploaded(1, "backend")]);
    var invariant = CommentComposer.Compose(content);
    var culture = System.Globalization.CultureInfo.CurrentCulture;

    try
    {
      System.Globalization.CultureInfo.CurrentCulture = System.Globalization.CultureInfo.GetCultureInfo("fr-FR");

      CommentComposer.Compose(content).ShouldBe(invariant);
    }
    finally
    {
      System.Globalization.CultureInfo.CurrentCulture = culture;
    }

    invariant.ShouldContain("%");
    invariant.ShouldNotMatch(@"\d,\d");
  }

  [Fact]
  public void Compose_Flags_ShouldBeCaseSensitiveAndInOrdinalOrder()
  {
    var totals = new CoverageTotals(1, 1, 0, 0, 0);

    var body = CommentComposer.Compose(Content(Layout("header, flags"), Comparison(flags: [Flag("unit", totals, totals), Flag("Unit", totals, totals), Flag("api_v2", totals, totals)])));

    string[] names = [.. body.Split('\n').Where(static line => line.StartsWith("| ", StringComparison.Ordinal) && line.Contains('`', StringComparison.Ordinal)).Select(static line => line.Split(' ')[1])];

    names.ShouldBe(["Unit", "api\\_v2", "unit"]);
  }

  [Fact]
  public void Compose_NoFlagToShow_ShouldWriteNoTable()
  {
    var body = CommentComposer.Compose(Content(Layout("header, flags"), Comparison(flags: [Frontend])));

    body.ShouldNotContain("| Flag |");
    body.ShouldNotContain("<details>");
  }

  [Fact]
  public void Compose_CarriedForwardFlagShown_ShouldSayWhereItComesFrom()
  {
    var config = Config(("layout", ConfigNode.FromText("header, flags")), ("show_carryforward_flags", ConfigNode.FromBoolean(true)));

    var linked = CommentComposer.Compose(Content(config, Comparison(flags: [Frontend]), sessions: [CarriedForward(1, OlderSha, "frontend")]));
    var unknown = CommentComposer.Compose(Content(config, Comparison(flags: [Frontend]), sessions: [Uploaded(1, "backend")]));

    linked.ShouldContain("| frontend | `70.02% <ø> (ø)` | Carriedforward from [`9f8e7d6`](https://coverage.example.com/github/octo/repo/commit/" + OlderSha.Value + ") |");
    unknown.ShouldContain("| frontend | `70.02% <ø> (ø)` | Carriedforward |");
  }

  // hide_project_coverage hides the project line and the diff table only.
  [Fact]
  public void Compose_HideProjectCoverage_ShouldKeepTheFlagsAndTheFiles()
  {
    var config = Config(("hide_project_coverage", ConfigNode.FromBoolean(true)));

    var body = CommentComposer.Compose(Content(config, Comparison(files: [Foo], flags: [Backend])));

    body.ShouldNotContain("```diff");
    body.ShouldContain("| backend |");
    body.ShouldContain("Foo.cs");
    body.ShouldEndWith("</sub>");
  }

  [Theory]
  [InlineData(0, 0, 0, "0 uploads</sub>")]
  [InlineData(1, 0, 0, "1 upload</sub>")]
  [InlineData(2, 1, 0, "3 uploads (1 carried forward)</sub>")]
  [InlineData(1, 0, 1, "1 upload · 1 upload failed</sub>")]
  [InlineData(1, 0, 2, "1 upload · 2 uploads failed</sub>")]
  public void Compose_Footer_ShouldCountTheUploads(int uploaded, int carried, int failed, string expected)
  {
    SessionCoverage[] sessions =
    [
      .. Enumerable.Range(1, uploaded).Select(static i => Uploaded(i, "a")),
      .. Enumerable.Range(1, carried).Select(i => CarriedForward(uploaded + i, OlderSha, "b")),
    ];

    var body = CommentComposer.Compose(Content(Layout("header, footer"), sessions: sessions, failedUploads: failed));

    body.ShouldEndWith(expected);
    body.ShouldContain("<sub>Δ = absolute &lt;relative&gt; (impact), ø = not affected, ? = missing data · Last update a1b2c3d...e4f5a6b · ");
  }

  [Fact]
  public void Compose_FooterWithoutBase_ShouldNameTheHeadOnly()
  {
    var body = CommentComposer.Compose(Content(Layout("header, footer"), withoutBase: true));

    body.ShouldContain(" · Last update e4f5a6b · ");
  }

  [Fact]
  public void Compose_LayoutWithoutFooter_ShouldWriteNone()
  {
    CommentComposer.Compose(Content(Layout("header, diff"))).ShouldNotContain("<sub>");
  }

  #endregion
}
