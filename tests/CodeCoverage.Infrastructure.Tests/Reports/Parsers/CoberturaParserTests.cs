// ------------------------------------------------------------------------------------------------
// <copyright file="CoberturaParserTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Text;
using System.Xml;

using CodeCoverage.Domain.Coverage;
using CodeCoverage.Infrastructure.Reports.Parsers;

namespace CodeCoverage.Infrastructure.Tests.Reports.Parsers;

public sealed class CoberturaParserTests
{
  #region Fields

  // The four fixtures are the real output of coverlet.msbuild 10.1.0 and ReportGenerator 5.5.3 on a
  // small multi-target sample; only the machine-specific prefix of their paths was rewritten. The
  // XML of the other tests is hand-written. The expected totals are those of Codecov's rules
  // (cobertura.py and merge.py of codecov/umbrella), run on the fixtures.
  private const string FileName = "A.cs";

  private readonly CoberturaParser _parser = new();

  #endregion

  #region Methods

  [Fact]
  public async Task Parse_ConditionCoverage_ShouldEmitBranchLine()
  {
    var lines = await ParseLinesAsync(
      """
      <line number="3" hits="4" branch="true" condition-coverage="50% (1/2)">
        <conditions>
          <condition number="8" type="jump" coverage="50%" />
          <condition number="9" type="jump" coverage="100%" />
        </conditions>
      </line>
      """);

    lines.ShouldBe([new LineCoverage(3, 4, 1, 2, false, ["8:jump"])]);
  }

  [Theory]
  [InlineData("""<line number="3" hits="2" branch="true" />""")]
  [InlineData("""<line number="3" hits="2" branch="true" condition-coverage="50%" />""")]
  [InlineData("""<line number="3" hits="2" branch="false" condition-coverage="50% (1/2)" />""")]
  [InlineData("""<line number="3" hits="2" condition-coverage="50% (1/2)" />""")]
  public async Task Parse_BranchTrueWithoutCondition_ShouldUseHits(string line)
  {
    var lines = await ParseLinesAsync(line);

    lines.ShouldBe([new LineCoverage(3, 2, 0, 0, false, null)]);
  }

  [Fact]
  public async Task Parse_MethodsSubtrees_ShouldCountTheirLines()
  {
    var lines = await ParseLinesAsync(
      """
      <methods>
        <method name="M" signature="()">
          <lines>
            <line number="1" hits="9" />
            <line number="2" hits="9" />
          </lines>
        </method>
      </methods>
      <lines>
        <line number="2" hits="1" />
      </lines>
      """);

    lines.ShouldBe(
    [
      new LineCoverage(1, 9, 0, 0, false, null),
      new LineCoverage(2, 9, 0, 0, false, null),
    ]);
  }

  // The shape of Istanbul: a function declaration, without branch data, in the methods element.
  [Theory]
  [InlineData(1, 2)]
  [InlineData(0, 1)]
  public async Task Parse_FunctionDeclarationOnBranchLine_ShouldFollowTheMerge(int functionHits, int expectedCovered)
  {
    var lines = await ParseLinesAsync(
      $"""
      <methods><method name="(anonymous_0)" hits="{functionHits}" signature="()V"><lines><line number="3" hits="{functionHits}" /></lines></method></methods>
      <lines><line number="3" hits="1" branch="true" condition-coverage="50% (1/2)" /></lines>
      """);

    lines.ShouldBe([new LineCoverage(3, 1, expectedCovered, 2, false, null)]);
  }

  // Codecov reads such a line twice and recounts it from the branches it misses: 1/4 becomes 2/4.
  [Fact]
  public async Task Parse_LineRepeatedInMethods_ShouldRecountFromMissingBranches()
  {
    const string Line =
      """
      <line number="5" hits="1" branch="True" condition-coverage="25% (1/4)">
        <conditions>
          <condition number="0" type="jump" coverage="50%" />
          <condition number="1" type="jump" coverage="0%" />
        </conditions>
      </line>
      """;

    var lines = await ParseLinesAsync($"<methods><method><lines>{Line}</lines></method></methods><lines>{Line}</lines>");

    lines.ShouldBe([new LineCoverage(5, 1, 2, 4, false, ["0:jump", "1:jump"])]);
  }

  [Fact]
  public async Task Parse_SameFileInSeveralClasses_ShouldAggregate()
  {
    var report = await ParseAsync(
      """
      <coverage line-rate="1">
        <packages>
          <package name="One">
            <classes>
              <class name="A" filename="A.cs"><lines><line number="1" hits="1" /><line number="3" hits="0" /></lines></class>
              <class name="B" filename="B.cs"><lines><line number="1" hits="5" /></lines></class>
            </classes>
          </package>
          <package name="Two">
            <classes>
              <class name="C" filename="A.cs"><lines><line number="2" hits="1" /><line number="3" hits="7" /></lines></class>
            </classes>
          </package>
        </packages>
      </coverage>
      """);

    report.Files.Select(file => file.RawPath).ShouldBe(["A.cs", "B.cs"]);
    report.Files[0].Lines.ShouldBe(
    [
      new LineCoverage(1, 1, 0, 0, false, null),
      new LineCoverage(2, 1, 0, 0, false, null),
      new LineCoverage(3, 7, 0, 0, false, null),
    ]);
  }

  [Fact]
  public async Task Parse_SameBranchLineInSeveralClasses_ShouldIntersectMissingBranches()
  {
    var lines = await ParseLinesAsync(
      """
      <lines>
        <line number="1" hits="1" branch="true" condition-coverage="50% (1/2)"><conditions><condition number="0" type="jump" coverage="0%" /></conditions></line>
        <line number="1" hits="1" branch="true" condition-coverage="50% (1/2)"><conditions><condition number="1" type="jump" coverage="0%" /></conditions></line>
      </lines>
      """);

    lines.ShouldBe([new LineCoverage(1, 1, 2, 2, false, [])]);
  }

  [Theory]
  [InlineData("coverlet-multitarget.xml", 23, 11, 5, 12)]
  [InlineData("coverlet-async.xml", 22, 11, 5, 12)]
  [InlineData("reportgenerator-merged.xml", 23, 11, 5, 12)]
  [InlineData("windows-paths.xml", 23, 11, 5, 12)]
  public async Task Parse_EachFixture_ShouldMatchCodecovTotals(string fixture, int hits, int misses, int partials, int branches)
  {
    var report = await ParseFixtureAsync(fixture);

    var totals = CoverageReport.Create(report.Files.Select(file => FileCoverage.Create(file.RawPath, file.Lines))).Totals;

    report.Format.ShouldBe(ReportFormat.Cobertura);
    report.Files.Count.ShouldBe(3);
    totals.ShouldBe(new CoverageTotals(hits, misses, partials, branches, 0));
  }

  [Fact]
  public async Task Parse_CoverletCapitalizedBranch_ShouldEmitBranchLine()
  {
    var report = await ParseFixtureAsync("coverlet-multitarget.xml");

    var calculator = report.Files.Single(file => file.RawPath == "Calculator.cs");

    calculator.Lines.Single(line => line.Line == 12).ShouldBe(new LineCoverage(12, 2, 1, 2, false, ["8:jump"]));
  }

  [Fact]
  public async Task Parse_MultiTargetFixture_ShouldKeepLinesOfBothTargets()
  {
    var report = await ParseFixtureAsync("coverlet-multitarget.xml");

    var calculator = report.Files.Single(file => file.RawPath == "Calculator.cs");

    calculator.Lines.Where(line => line.Line is 28 or 30).Select(line => line.Hits).ShouldBe([1L, 1L]);
  }

  [Fact]
  public async Task Parse_WindowsPaths_ShouldKeepPathsAndRootsAsWritten()
  {
    var report = await ParseFixtureAsync("windows-paths.xml");

    report.SourceRoots.ShouldBe(["D:/a/sample/sample/src/Sample/"]);
    report.Files.Select(file => file.RawPath).ShouldContain(@"D:\a\sample\sample\src\Sample\Internal\Helper.cs");
  }

  [Fact]
  public async Task Parse_DeterministicFixture_ShouldKeepTheMappedPrefix()
  {
    var report = await ParseFixtureAsync("coverlet-async.xml");

    report.SourceRoots.ShouldBeEmpty();
    report.Files.Select(file => file.RawPath).ShouldContain("/_/src/Sample/Worker.cs");
  }

  [Fact]
  public async Task Parse_SourcesWithoutSpaceBetween_ShouldCollectEachOne()
  {
    var report = await ParseAsync(
      """<coverage line-rate="1"><sources><source>/a</source><source>/b</source><source> </source><source /></sources><packages /></coverage>""");

    report.SourceRoots.ShouldBe(["/a", "/b"]);
  }

  [Fact]
  public async Task Parse_ZeroOverZero_ShouldEmitAHitWithoutBranchData()
  {
    var lines = await ParseLinesAsync("""<line number="1" hits="0" branch="true" condition-coverage="0% (0/0)" />""");

    lines.ShouldBe([new LineCoverage(1, 1, 0, 0, false, null)]);
  }

  [Fact]
  public async Task Parse_MissingBranchesAttribute_ShouldWinOverConditions()
  {
    var lines = await ParseLinesAsync(
      """<line number="1" hits="1" branch="true" condition-coverage="50% (1/2)" missing-branches="30"><conditions><condition number="0" type="jump" coverage="0%" /></conditions></line>""");

    lines.ShouldBe([new LineCoverage(1, 1, 1, 2, false, ["30"])]);
  }

  [Theory]
  [InlineData("exit", new[] { "exit" })]
  [InlineData("exit,exit,exit", new[] { "loop", "exit" })]
  [InlineData("12,exit", new[] { "12", "exit" })]
  public async Task Parse_MissingBranchesAttribute_ShouldFollowCodecovForExits(string attribute, string[] expected)
  {
    var lines = await ParseLinesAsync($"""<line number="1" hits="1" branch="true" condition-coverage="0% (0/2)" missing-branches="{attribute}" />""");

    lines.ShouldHaveSingleItem().MissingBranches.ShouldBe(expected);
  }

  [Fact]
  public async Task Parse_MissedLineWithFewerConditions_ShouldNumberTheOtherBranches()
  {
    var lines = await ParseLinesAsync(
      """<line number="1" hits="0" branch="true" condition-coverage="0% (0/3)"><conditions><condition number="0" type="jump" coverage="0%" /></conditions></line>""");

    lines.ShouldBe([new LineCoverage(1, 0, 0, 3, false, ["0:jump", "1", "2"])]);
  }

  [Fact]
  public async Task Parse_MissedLineClaimingHugeBranchCount_ShouldNotNameThem()
  {
    var lines = await ParseLinesAsync("""<line number="1" hits="0" branch="true" condition-coverage="0% (0/2000000000)" />""");

    lines.ShouldBe([new LineCoverage(1, 0, 0, 2000000000, false, null)]);
  }

  [Fact]
  public async Task Parse_PartialLineWithoutConditions_ShouldNotNameBranches()
  {
    var lines = await ParseLinesAsync("""<line number="1" hits="0" branch="true" condition-coverage="50% (1/2)" />""");

    lines.ShouldBe([new LineCoverage(1, 0, 1, 2, false, null)]);
  }

  [Theory]
  [InlineData("undefined")]
  [InlineData("0")]
  [InlineData("-3")]
  public async Task Parse_LineNumberNotCoverable_ShouldSkipTheLine(string number)
  {
    var lines = await ParseLinesAsync($"""<line number="{number}" hits="1" /><line number="9" hits="1" />""");

    lines.ShouldBe([new LineCoverage(9, 1, 0, 0, false, null)]);
  }

  [Theory]
  [InlineData("3", 3)]
  [InlineData("1.0", 1)]
  [InlineData("2.9", 2)]
  [InlineData("0.5", 0)]
  [InlineData("1e3", 1000)]
  [InlineData("-4", 0)]
  [InlineData("-0.5", 0)]
  [InlineData("99999999999999999999", long.MaxValue)]
  [InlineData("1e300", long.MaxValue)]
  public async Task Parse_Hits_ShouldReadThemLikeCodecovWithinBounds(string hits, long expected)
  {
    var lines = await ParseLinesAsync($"""<line number="1" hits="{hits}" />""");

    lines.ShouldHaveSingleItem().Hits.ShouldBe(expected);
  }

  [Fact]
  public async Task Parse_BranchLineWithoutHits_ShouldKeepTheBranchData()
  {
    var lines = await ParseLinesAsync("""<line number="1" branch="true" condition-coverage="100% (2/2)" />""");

    lines.ShouldBe([new LineCoverage(1, 0, 2, 2, false, null)]);
  }

  [Theory]
  [InlineData("""<line hits="1" />""")]
  [InlineData("""<line number="x" hits="1" />""")]
  [InlineData("""<line number="1.0" hits="1" />""")]
  [InlineData("""<line number="1" />""")]
  [InlineData("""<line number="1" hits="x" />""")]
  [InlineData("""<line number="1" hits="NaN" />""")]
  [InlineData("""<line number="1" hits="Infinity" />""")]
  [InlineData("""<line number="1" hits="1"><condition type="jump" coverage="0%" /></line>""")]
  public async Task Parse_InvalidLine_ShouldThrowFormatException(string line)
  {
    await Should.ThrowAsync<FormatException>(() => ParseLinesAsync(line));
  }

  [Fact]
  public async Task Parse_ClassWithoutFilename_ShouldThrowFormatException()
  {
    await Should.ThrowAsync<FormatException>(() => ParseAsync("""<coverage line-rate="1"><class name="A" /></coverage>"""));
  }

  [Fact]
  public async Task Parse_ClassWithEmptyFilename_ShouldSkipItsContent()
  {
    var report = await ParseAsync("""<coverage line-rate="1"><class filename=""><lines><line number="x" /></lines></class></coverage>""");

    report.Files.ShouldBeEmpty();
  }

  [Theory]
  [InlineData("""<class filename="A.cs" />""")]
  [InlineData("""<class filename="A.cs"></class>""")]
  [InlineData("""<class filename="A.cs"><methods /><lines /></class>""")]
  public async Task Parse_ClassWithoutLines_ShouldNotEmitAFile(string @class)
  {
    var report = await ParseAsync($"""<coverage line-rate="1">{@class}</coverage>""");

    report.Files.ShouldBeEmpty();
  }

  [Fact]
  public async Task Parse_MinifiedReport_ShouldReadEachLine()
  {
    var lines = await ParseLinesAsync("""<methods /><lines><line number="1" hits="1"><conditions /></line><line number="2" hits="1" /></lines>""");

    lines.Select(line => line.Line).ShouldBe([1, 2]);
  }

  [Fact]
  public async Task Parse_XxePayload_ShouldThrowXmlException()
  {
    var path = Path.Combine(AppContext.BaseDirectory, "fixtures", "reports", "security", "xxe.xml");

    await using var report = File.OpenRead(path);

    await Should.ThrowAsync<XmlException>(() => _parser.ParseAsync(report, TestContext.Current.CancellationToken));
  }

  [Theory]
  [InlineData("")]
  [InlineData("<coverage>")]
  public async Task Parse_MalformedXml_ShouldThrowXmlException(string xml)
  {
    await Should.ThrowAsync<XmlException>(() => ParseAsync(xml));
  }

  [Fact]
  public async Task Parse_CancelledToken_ShouldThrowOperationCanceledException()
  {
    using var report = new MemoryStream(Encoding.UTF8.GetBytes("""<coverage line-rate="1" />"""));

    await Should.ThrowAsync<OperationCanceledException>(() => _parser.ParseAsync(report, new CancellationToken(canceled: true)));
  }

  [Fact]
  public async Task Parse_NullReport_ShouldThrowArgumentNullException()
  {
    await Should.ThrowAsync<ArgumentNullException>(() => _parser.ParseAsync(null!, TestContext.Current.CancellationToken));
  }

  [Fact]
  public void Format_ShouldBeCobertura()
  {
    _parser.Format.ShouldBe(ReportFormat.Cobertura);
  }

  private async Task<IReadOnlyList<LineCoverage>> ParseLinesAsync(string content)
  {
    var report = await ParseAsync($"""<coverage line-rate="1"><packages><package><classes><class filename="{FileName}">{content}</class></classes></package></packages></coverage>""");

    return report.Files.Count == 0 ? [] : report.Files.ShouldHaveSingleItem().Lines;
  }

  private async Task<ParsedReport> ParseAsync(string xml)
  {
    using var report = new MemoryStream(Encoding.UTF8.GetBytes(xml));

    return await _parser.ParseAsync(report, TestContext.Current.CancellationToken);
  }

  private async Task<ParsedReport> ParseFixtureAsync(string name)
  {
    var path = Path.Combine(AppContext.BaseDirectory, "fixtures", "reports", "cobertura", name);

    await using var report = File.OpenRead(path);

    return await _parser.ParseAsync(report, TestContext.Current.CancellationToken);
  }

  #endregion
}
