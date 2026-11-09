// ------------------------------------------------------------------------------------------------
// <copyright file="OpenCoverParserTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Text;
using System.Xml;

using Microsoft.Extensions.DependencyInjection;

using CodeCoverage.Domain.Coverage;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Infrastructure.Reports.Parsers;

namespace CodeCoverage.Infrastructure.Tests.Reports.Parsers;

public sealed class OpenCoverParserTests
{
  #region Fields

  // The three fixtures are the real output of coverlet.msbuild 10.1.0 on a small sample of two
  // modules; only the machine-specific prefix of their paths was rewritten. coverlet-multitarget
  // merges a net8.0 and a net10.0 run, coverlet-async is a Release build, windows-paths a net8.0 one.
  // The XML of the other tests is hand-written. The expected totals are those of Codecov's rules
  // (csharp.py and merge.py of codecov/umbrella), applied to the fixtures.
  private const string FileName = "A.cs";

  private readonly OpenCoverParser _parser = new();

  #endregion

  #region Methods

  [Fact]
  public async Task Parse_SkippedModule_ShouldIgnore()
  {
    var report = await ParseAsync(
      """
      <CoverageSession>
        <Modules>
          <Module skippedDueTo="Filter" hash="AA"><ModulePath>Skipped.dll</ModulePath><ModuleTime /><ModuleName>Skipped</ModuleName><Classes /></Module>
          <Module hash="BB">
            <Files><File uid="1" fullPath="A.cs" /></Files>
            <Classes><Class><Methods><Method><FileRef uid="1" /><SequencePoints><SequencePoint vc="1" sl="1" el="1" bec="0" bev="0" /></SequencePoints></Method></Methods></Class></Classes>
          </Module>
        </Modules>
      </CoverageSession>
      """);

    report.Files.ShouldHaveSingleItem().RawPath.ShouldBe(FileName);
  }

  // Codecov never reads skippedDueTo: what a skipped element still carries counts.
  [Theory]
  [InlineData("""<Module skippedDueTo="Filter">""", """<Method>""")]
  [InlineData("""<Module>""", """<Method skippedDueTo="Attribute">""")]
  public async Task Parse_SkippedDueToWithSequencePoints_ShouldCountThem(string module, string method)
  {
    var report = await ParseAsync(
      $"""<CoverageSession>{module}<Files><File uid="1" fullPath="A.cs" /></Files>{method}<FileRef uid="1" /><SequencePoint vc="1" sl="1" el="1" /></Method></Module></CoverageSession>""");

    report.Files.ShouldHaveSingleItem().Lines.ShouldBe([new LineCoverage(1, 1, 0, 0, false, null)]);
  }

  [Fact]
  public async Task Parse_SequencePointSpan_ShouldApplyMaxToEachLine()
  {
    var lines = await ParseLinesAsync(
      """
      <SequencePoint vc="2" sl="3" el="5" bec="0" bev="0" />
      <SequencePoint vc="7" sl="4" el="4" bec="0" bev="0" />
      <SequencePoint vc="1" sl="5" el="5" bec="0" bev="0" />
      """);

    lines.ShouldBe(
    [
      new LineCoverage(3, 2, 0, 0, false, null),
      new LineCoverage(4, 7, 0, 0, false, null),
      new LineCoverage(5, 2, 0, 0, false, null),
    ]);
  }

  [Fact]
  public async Task Parse_UncoveredBranchPoints_ShouldProduceMissingIds()
  {
    var lines = await ParseLinesAsync(
      """
      <SequencePoint vc="4" sl="3" el="3" bec="3" bev="1" />
      <BranchPoint vc="1" offset="7" offsetend="9" sl="3" />
      <BranchPoint vc="0" offset="7" offsetend="18" sl="3" />
      <BranchPoint vc="0" offset="20" sl="3" />
      """);

    lines.ShouldBe([new LineCoverage(3, 4, 1, 3, false, ["7:18", "20"])]);
  }

  [Fact]
  public async Task Parse_BranchSpan_ShouldNameMissingBranchesOnTheirOwnLine()
  {
    var lines = await ParseLinesAsync(
      """
      <BranchPoint vc="0" offset="1" offsetend="2" sl="4" />
      <SequencePoint vc="1" sl="3" el="4" bec="2" bev="1" />
      """);

    lines.ShouldBe(
    [
      new LineCoverage(3, 1, 1, 2, false, null),
      new LineCoverage(4, 1, 1, 2, false, ["1:2"]),
    ]);
  }

  [Fact]
  public async Task Parse_EndBeforeStart_ShouldKeepOnlyTheStartLine()
  {
    var lines = await ParseLinesAsync("""<SequencePoint vc="2" sl="10" el="6" bec="2" bev="1" />""");

    lines.ShouldBe([new LineCoverage(10, 2, 1, 2, false, null)]);
  }

  // The scenario of Codecov's own unit test (test_csharp.py), with its expected lines and totals.
  [Fact]
  public async Task Parse_CodecovScenario_ShouldGiveTheSameTotals()
  {
    var report = await ParseAsync(
      """
      <CoverageSession>
        <File uid="1" fullPath="ignored.cs" />
        <File uid="3" fullPath="source.cs" />
        <Method>
          <FileRef uid="3" />
          <SequencePoint vc="2" sl="1" el="1" bec="2" bev="1" />
          <SequencePoint vc="2" sl="2" el="2" bec="0" bev="0" />
          <SequencePoint vc="0" sl="3" el="5" bec="0" bev="0" />
          <SequencePoint vc="0" sl="6" el="6" bec="0" bev="0" />
          <SequencePoint vc="2" sl="10" el="6" bec="2" bev="1" />
          <SequencePoint />
          <MethodPoint vc="3" sl="4" el="1" bec="0" bev="0" />
          <BranchPoint vc="3" offset="0" offsetend="1" sl="10" />
          <BranchPoint vc="0" offset="1" offsetend="2" sl="10" />
        </Method>
        <Method>
          <FileRef uid="3" />
          <SequencePoint vc="1" sl="6" el="6" bec="0" bev="0" />
          <SequencePoint vc="2" sl="1" el="1" bec="2" bev="2" />
        </Method>
      </CoverageSession>
      """);

    var file = report.Files.ShouldHaveSingleItem();
    var totals = CoverageReport.Create([FileCoverage.Create(file.RawPath, file.Lines)]).Totals;

    file.RawPath.ShouldBe("source.cs");
    file.Lines.Single(line => line.Line == 1).ShouldBe(new LineCoverage(1, 2, 2, 2, false, null));
    file.Lines.Single(line => line.Line == 6).ShouldBe(new LineCoverage(6, 1, 0, 0, false, null));
    file.Lines.Single(line => line.Line == 10).ShouldBe(new LineCoverage(10, 2, 1, 2, false, ["1:2"]));
    totals.ShouldBe(new CoverageTotals(3, 3, 1, 2, 0));
  }

  [Fact]
  public async Task Parse_PlainLineMergedWithBranchLine_ShouldFollowTheMerge()
  {
    var lines = await ParseLinesAsync(
      """
      <SequencePoint vc="1" sl="1" el="1" bec="2" bev="1" />
      <SequencePoint vc="1" sl="1" el="1" bec="0" bev="0" />
      <SequencePoint vc="1" sl="2" el="2" bec="2" bev="1" />
      <SequencePoint vc="0" sl="2" el="2" bec="0" bev="0" />
      <BranchPoint vc="0" offset="4" offsetend="6" sl="1" />
      """);

    lines.ShouldBe(
    [
      new LineCoverage(1, 1, 2, 2, false, []),
      new LineCoverage(2, 1, 1, 2, false, null),
    ]);
  }

  // A named difference: Codecov depends on the order of the sequence points there and gives 1/2.
  [Fact]
  public async Task Parse_ExecutedPlainPointBetweenBranchPoints_ShouldStayFullyCovered()
  {
    var lines = await ParseLinesAsync(
      """
      <SequencePoint vc="1" sl="1" el="1" bec="2" bev="1" />
      <SequencePoint vc="1" sl="1" el="1" bec="0" bev="0" />
      <SequencePoint vc="1" sl="1" el="1" bec="2" bev="1" />
      <BranchPoint vc="0" offset="4" offsetend="6" sl="1" />
      """);

    lines.ShouldBe([new LineCoverage(1, 1, 2, 2, false, [])]);
  }

  [Theory]
  [InlineData("""<BranchPoint vc="1" offset="1" offsetend="2" sl="1" />""")]
  [InlineData("""<BranchPoint vc="00" offset="1" offsetend="2" sl="1" />""")]
  [InlineData("""<BranchPoint vc="0" offset="1" offsetend="2" />""")]
  [InlineData("""<BranchPoint vc="0" offset="1" offsetend="2" sl="9" />""")]
  public async Task Parse_BranchPointNotMissingOnTheLine_ShouldNotBeNamed(string branch)
  {
    var lines = await ParseLinesAsync($"""<SequencePoint vc="1" sl="1" el="1" bec="2" bev="1" />{branch}""");

    lines.ShouldBe([new LineCoverage(1, 1, 1, 2, false, null)]);
  }

  [Theory]
  [InlineData("""<SequencePoint vc="3" sl="1" el="1" />""", 3)]
  [InlineData("""<SequencePoint vc="3" sl="1" el="1" bec="0" />""", 3)]
  [InlineData("""<SequencePoint vc="0" sl="1" el="1" bec="0" bev="0" />""", 0)]
  public async Task Parse_SequencePointWithoutBranch_ShouldUseVisitCount(string point, long expected)
  {
    var lines = await ParseLinesAsync(point);

    lines.ShouldBe([new LineCoverage(1, expected, 0, 0, false, null)]);
  }

  [Theory]
  [InlineData("0")]
  [InlineData("9")]
  public async Task Parse_SequencePointWithBranches_ShouldIgnoreVisitCountForTheState(string visits)
  {
    var lines = await ParseLinesAsync($"""<SequencePoint vc="{visits}" sl="1" el="1" bec="2" bev="0" />""");

    CoverageMath.Classify(lines.ShouldHaveSingleItem()).ShouldBe(LineState.Miss);
  }

  [Theory]
  [InlineData("""<SequencePoint />""")]
  [InlineData("""<SequencePoint vc="1" sl="1" />""")]
  [InlineData("""<SequencePoint vc="1" el="1" />""")]
  [InlineData("""<SequencePoint vc="1" sl="" el="1" />""")]
  [InlineData("""<SequencePoint vc="x" el="" sl="1" />""")]
  [InlineData("""<MethodPoint vc="1" sl="1" el="1" />""")]
  public async Task Parse_PointWithoutLines_ShouldBeIgnored(string point)
  {
    var lines = await ParseLinesAsync(point);

    lines.ShouldBeEmpty();
  }

  [Theory]
  [InlineData("3", 3)]
  [InlineData("-4", 0)]
  [InlineData("99999999999999999999", long.MaxValue)]
  [InlineData("-99999999999999999999", 0)]
  public async Task Parse_VisitCount_ShouldStayWithinBounds(string visits, long expected)
  {
    var lines = await ParseLinesAsync($"""<SequencePoint vc="{visits}" sl="1" el="1" />""");

    lines.ShouldHaveSingleItem().Hits.ShouldBe(expected);
  }

  [Theory]
  [InlineData("""<SequencePoint sl="1" el="1" />""")]
  [InlineData("""<SequencePoint vc="x" sl="1" el="1" />""")]
  [InlineData("""<SequencePoint vc="1.5" sl="1" el="1" />""")]
  [InlineData("""<SequencePoint vc="1" sl="x" el="1" />""")]
  [InlineData("""<SequencePoint vc="1" sl="1" el="x" />""")]
  [InlineData("""<SequencePoint vc="1" sl="99999999999" el="1" />""")]
  [InlineData("""<SequencePoint vc="1" sl="0" el="0" />""")]
  [InlineData("""<SequencePoint vc="1" sl="-2" el="1" />""")]
  [InlineData("""<SequencePoint vc="1" sl="1" el="1" bec="2" />""")]
  [InlineData("""<SequencePoint vc="1" sl="1" el="1" bec="x" bev="1" />""")]
  [InlineData("""<SequencePoint vc="1" sl="1" el="1" bec="-2" bev="1" />""")]
  [InlineData("""<BranchPoint offset="1" sl="1" />""")]
  [InlineData("""<BranchPoint vc="0" sl="1" />""")]
  [InlineData("""<BranchPoint vc="0" offsetend="2" sl="1" />""")]
  [InlineData("""<BranchPoint vc="0" offset="1" sl="x" />""")]
  public async Task Parse_InvalidPoint_ShouldThrowFormatException(string point)
  {
    await Should.ThrowAsync<FormatException>(() => ParseLinesAsync(point));
  }

  [Theory]
  [InlineData("""<File uid="1" />""")]
  [InlineData("""<File fullPath="A.cs" />""")]
  [InlineData("""<Method><FileRef /></Method>""")]
  public async Task Parse_InvalidFileOrReference_ShouldThrowFormatException(string element)
  {
    await Should.ThrowAsync<FormatException>(() => ParseAsync($"<CoverageSession>{element}</CoverageSession>"));
  }

  [Fact]
  public async Task Parse_FileWithEmptyPath_ShouldNotReplaceTheFileOfItsUid()
  {
    var report = await ParseAsync(
      """<CoverageSession><File uid="1" fullPath="A.cs" /><File fullPath="" /><File uid="1" fullPath="" /><Method><FileRef uid="1" /><SequencePoint vc="1" sl="1" el="1" /></Method></CoverageSession>""");

    report.Files.ShouldHaveSingleItem().RawPath.ShouldBe(FileName);
  }

  // The table of Codecov covers the whole report: a uid declared again moves all its lines.
  [Fact]
  public async Task Parse_UidDeclaredInSeveralModules_ShouldAttributeToTheLastFile()
  {
    var report = await ParseAsync(
      """
      <CoverageSession>
        <Module><File uid="1" fullPath="A.cs" /><Method><FileRef uid="1" /><SequencePoint vc="1" sl="1" el="1" /></Method></Module>
        <Module><File uid="1" fullPath="B.cs" /><Method><FileRef uid="1" /><SequencePoint vc="1" sl="2" el="2" /></Method></Module>
      </CoverageSession>
      """);

    var file = report.Files.ShouldHaveSingleItem();

    file.RawPath.ShouldBe("B.cs");
    file.Lines.Select(line => line.Line).ShouldBe([1, 2]);
  }

  [Fact]
  public async Task Parse_FileDeclaredAfterItsMethods_ShouldStillBeFound()
  {
    var report = await ParseAsync(
      """<CoverageSession><Method><FileRef uid="1" /><SequencePoint vc="1" sl="1" el="1" /></Method><File uid="1" fullPath="A.cs" /></CoverageSession>""");

    report.Files.ShouldHaveSingleItem().RawPath.ShouldBe(FileName);
  }

  [Fact]
  public async Task Parse_SamePathUnderSeveralUids_ShouldMergeLines()
  {
    var report = await ParseAsync(
      """
      <CoverageSession>
        <File uid="1" fullPath="A.cs" />
        <File uid="2" fullPath="A.cs" />
        <Method><FileRef uid="1" /><SequencePoint vc="1" sl="1" el="1" /><SequencePoint vc="0" sl="2" el="2" /></Method>
        <Method><FileRef uid="2" /><SequencePoint vc="5" sl="2" el="2" /><SequencePoint vc="3" sl="3" el="3" /></Method>
      </CoverageSession>
      """);

    report.Files.ShouldHaveSingleItem().Lines.ShouldBe(
    [
      new LineCoverage(1, 1, 0, 0, false, null),
      new LineCoverage(2, 5, 0, 0, false, null),
      new LineCoverage(3, 3, 0, 0, false, null),
    ]);
  }

  [Theory]
  [InlineData("""<Method><SequencePoint vc="x" sl="1" el="1" /><BranchPoint /></Method>""")]
  [InlineData("""<Method><FileRef uid="9" /><SequencePoint vc="1" sl="1" el="1" /></Method>""")]
  public async Task Parse_MethodWithoutKnownFile_ShouldBeIgnored(string method)
  {
    var report = await ParseAsync($"""<CoverageSession><File uid="1" fullPath="A.cs" />{method}</CoverageSession>""");

    report.Files.ShouldBeEmpty();
  }

  [Fact]
  public async Task Parse_FileWithoutLines_ShouldNotEmitAFile()
  {
    var report = await ParseAsync(
      """<CoverageSession><File uid="1" fullPath="A.cs" /><Method><FileRef uid="1" /><SequencePoints /></Method></CoverageSession>""");

    report.Files.ShouldBeEmpty();
  }

  [Fact]
  public async Task Parse_ReportClaimingHugeSpan_ShouldThrowFormatException()
  {
    await Should.ThrowAsync<FormatException>(() => ParseLinesAsync("""<SequencePoint vc="1" sl="1" el="2147483647" />"""));
  }

  [Fact]
  public async Task Parse_ManySpansOverTheBudget_ShouldThrowFormatException()
  {
    var points = string.Concat(Enumerable.Repeat("""<SequencePoint vc="1" sl="1" el="600000" />""", 2));

    await Should.ThrowAsync<FormatException>(() => ParseLinesAsync(points));
  }

  [Fact]
  public async Task Parse_SpanReachingTheLastLine_ShouldNotOverflow()
  {
    var lines = await ParseLinesAsync("""<SequencePoint vc="1" sl="2147483646" el="2147483647" />""");

    lines.Select(line => line.Line).ShouldBe([2147483646, 2147483647]);
  }

  [Theory]
  [InlineData("coverlet-multitarget.xml", 43, 18, 9, 15)]
  [InlineData("coverlet-async.xml", 24, 11, 8, 14)]
  [InlineData("windows-paths.xml", 42, 18, 9, 15)]
  public async Task Parse_EachFixture_ShouldMatchCodecovTotals(string fixture, int hits, int misses, int partials, int branches)
  {
    var report = await ParseFixtureAsync(fixture);

    var totals = CoverageReport.Create(report.Files.Select(file => FileCoverage.Create(file.RawPath, file.Lines))).Totals;

    report.Format.ShouldBe(ReportFormat.OpenCover);
    report.SourceRoots.ShouldBeEmpty();
    report.Files.Count.ShouldBe(4);
    totals.ShouldBe(new CoverageTotals(hits, misses, partials, branches, 0));
  }

  [Fact]
  public async Task Parse_CoverletBranchPoints_ShouldNameMissingBranches()
  {
    var report = await ParseFixtureAsync("coverlet-multitarget.xml");

    var calculator = report.Files.Single(file => file.RawPath.EndsWith("/Calculator.cs", StringComparison.Ordinal));

    calculator.Lines.Single(line => line.Line == 12).ShouldBe(new LineCoverage(12, 2, 2, 4, false, ["16:24", "27:29"]));
  }

  [Fact]
  public async Task Parse_MultiTargetFixture_ShouldKeepLinesOfBothTargets()
  {
    var report = await ParseFixtureAsync("coverlet-multitarget.xml");

    var calculator = report.Files.Single(file => file.RawPath.EndsWith("/Calculator.cs", StringComparison.Ordinal));

    calculator.Lines.Where(line => line.Line is 31 or 33).Select(line => line.Hits).ShouldBe([1L, 1L]);
  }

  [Fact]
  public async Task Parse_WindowsPaths_ShouldKeepPathsAsWritten()
  {
    var report = await ParseFixtureAsync("windows-paths.xml");

    report.Files.Select(file => file.RawPath).ShouldContain(@"D:\a\sample\sample\src\Sample\Internal\Helper.cs");
  }

  [Fact]
  public async Task Parse_SeveralModulesFixture_ShouldReadEachModule()
  {
    var report = await ParseFixtureAsync("coverlet-multitarget.xml");

    report.Files.Select(file => file.RawPath).ShouldContain("/home/runner/work/sample/sample/src/Sample.Extras/Extras.cs");
  }

  [Fact]
  public async Task Parse_MinifiedReport_ShouldReadEachElement()
  {
    var report = await ParseAsync(
      """<CoverageSession><File uid="1" fullPath="A.cs" /><File uid="2" fullPath="B.cs" /><Method><FileRef uid="1" /><SequencePoint vc="1" sl="1" el="1" /></Method><Method><FileRef uid="2" /><SequencePoint vc="1" sl="1" el="1" /></Method></CoverageSession>""");

    report.Files.Select(file => file.RawPath).ShouldBe(["A.cs", "B.cs"]);
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
  [InlineData("<CoverageSession>")]
  [InlineData("""<CoverageSession><Method><FileRef uid="1"></Method></CoverageSession>""")]
  public async Task Parse_MalformedXml_ShouldThrowXmlException(string xml)
  {
    await Should.ThrowAsync<XmlException>(() => ParseAsync(xml));
  }

  [Fact]
  public async Task Parse_CancelledToken_ShouldThrowOperationCanceledException()
  {
    using var report = new MemoryStream(Encoding.UTF8.GetBytes("<CoverageSession />"));

    await Should.ThrowAsync<OperationCanceledException>(() => _parser.ParseAsync(report, new CancellationToken(canceled: true)));
  }

  [Fact]
  public async Task Parse_NullReport_ShouldThrowArgumentNullException()
  {
    await Should.ThrowAsync<ArgumentNullException>(() => _parser.ParseAsync(null!, TestContext.Current.CancellationToken));
  }

  [Fact]
  public void Format_ShouldBeOpenCover()
  {
    _parser.Format.ShouldBe(ReportFormat.OpenCover);
  }

  [Fact]
  public async Task AddInfrastructure_ShouldRegisterTheOpenCoverParser()
  {
    var services = new ServiceCollection();

    services.AddInfrastructure();

    await using var provider = services.BuildServiceProvider();

    provider.GetRequiredService<IReportParserCatalog>().Find(ReportFormat.OpenCover).ShouldBeOfType<OpenCoverParser>();
  }

  private async Task<IReadOnlyList<LineCoverage>> ParseLinesAsync(string content)
  {
    var report = await ParseAsync($"""<CoverageSession><File uid="1" fullPath="{FileName}" /><Method><FileRef uid="1" />{content}</Method></CoverageSession>""");

    return report.Files.Count == 0 ? [] : report.Files.ShouldHaveSingleItem().Lines;
  }

  private async Task<ParsedReport> ParseAsync(string xml)
  {
    using var report = new MemoryStream(Encoding.UTF8.GetBytes(xml));

    return await _parser.ParseAsync(report, TestContext.Current.CancellationToken);
  }

  private async Task<ParsedReport> ParseFixtureAsync(string name)
  {
    var path = Path.Combine(AppContext.BaseDirectory, "fixtures", "reports", "opencover", name);

    await using var report = File.OpenRead(path);

    return await _parser.ParseAsync(report, TestContext.Current.CancellationToken);
  }

  #endregion
}
