// ------------------------------------------------------------------------------------------------
// <copyright file="CloverParserTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Text;
using System.Xml;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using CodeCoverage.Domain.Coverage;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Infrastructure.Reports.Parsers;

namespace CodeCoverage.Infrastructure.Tests.Reports.Parsers;

public sealed class CloverParserTests
{
  #region Fields

  // istanbul.xml is the real output of nyc 17.1.0 on a small sample; only the machine-specific prefix
  // of its paths was rewritten. The XML of the other tests is hand-written. The expected totals are
  // those of Codecov's rules (clover.py and merge.py of codecov/umbrella), run on the fixture.
  private const string FileName = "a.js";

  private readonly CloverParser _parser = new();

  #endregion

  #region Methods

  [Fact]
  public async Task Parse_CondBothZero_ShouldBeZeroOfTwo()
  {
    var lines = await ParseLinesAsync("""<line num="3" type="cond" truecount="0" falsecount="0" />""");

    lines.ShouldBe([new LineCoverage(3, 0, 0, 2, false, null)]);
  }

  [Theory]
  [InlineData(1, 0)]
  [InlineData(0, 1)]
  [InlineData(2, 0)]
  [InlineData(0, 2)]
  public async Task Parse_CondOneCountZero_ShouldBeOneOfTwo(int trueCount, int falseCount)
  {
    var lines = await ParseLinesAsync($"""<line num="3" type="cond" truecount="{trueCount}" falsecount="{falseCount}" />""");

    lines.ShouldBe([new LineCoverage(3, 0, 1, 2, false, null)]);
  }

  [Theory]
  [InlineData(1, 1)]
  [InlineData(3, 1)]
  [InlineData(-1, 1)]
  public async Task Parse_CondNoCountZero_ShouldBeTwoOfTwo(int trueCount, int falseCount)
  {
    var lines = await ParseLinesAsync($"""<line num="3" type="cond" truecount="{trueCount}" falsecount="{falseCount}" />""");

    lines.ShouldBe([new LineCoverage(3, 0, 2, 2, false, null)]);
  }

  [Fact]
  public async Task Parse_CondWithCount_ShouldKeepItsHits()
  {
    var lines = await ParseLinesAsync("""<line num="3" count="7" type="cond" truecount="2" falsecount="0" />""");

    lines.ShouldBe([new LineCoverage(3, 7, 1, 2, false, null)]);
  }

  [Fact]
  public async Task Parse_CondWithInvalidCount_ShouldIgnoreIt()
  {
    var lines = await ParseLinesAsync("""<line num="3" count="x" type="cond" truecount="1" falsecount="1" />""");

    lines.ShouldBe([new LineCoverage(3, 0, 2, 2, false, null)]);
  }

  [Fact]
  public async Task Parse_MethodLine_ShouldCountAsLine()
  {
    var lines = await ParseLinesAsync(
      """
      <line num="5" type="method" name="send" crap="125.96" count="1" />
      <line num="6" type="method" name="other" count="0" />
      """);

    lines.ShouldBe(
    [
      new LineCoverage(5, 1, 0, 0, true, null),
      new LineCoverage(6, 0, 0, 0, true, null),
    ]);
  }

  [Theory]
  [InlineData("stmt")]
  [InlineData("other")]
  public async Task Parse_OtherType_ShouldUseCount(string type)
  {
    var lines = await ParseLinesAsync($"""<line num="8" type="{type}" count="4" />""");

    lines.ShouldBe([new LineCoverage(8, 4, 0, 0, false, null)]);
  }

  [Theory]
  [InlineData("""<line num="8" type="stmt" />""")]
  [InlineData("""<line num="8" type="stmt" count="" />""")]
  public async Task Parse_MissingOrEmptyCount_ShouldBeZeroHits(string line)
  {
    var lines = await ParseLinesAsync(line);

    lines.ShouldBe([new LineCoverage(8, 0, 0, 0, false, null)]);
  }

  [Theory]
  [InlineData("-3", 0)]
  [InlineData("99999999999999999999999", long.MaxValue)]
  public async Task Parse_CountOutOfRange_ShouldBeBounded(string count, long expected)
  {
    var lines = await ParseLinesAsync($"""<line num="8" type="stmt" count="{count}" />""");

    lines.ShouldBe([new LineCoverage(8, expected, 0, 0, false, null)]);
  }

  [Theory]
  [InlineData("0")]
  [InlineData("-1")]
  public async Task Parse_LineNumberBelowOne_ShouldBeSkippedWhateverItsType(string number)
  {
    var lines = await ParseLinesAsync($"""<line num="1" type="stmt" count="1" /><line num="{number}" />""");

    lines.Select(line => line.Line).ShouldBe([1]);
  }

  [Fact]
  public async Task Parse_SameLineTwice_ShouldMergeThem()
  {
    var lines = await ParseLinesAsync(
      """
      <line num="3" type="method" count="0" />
      <line num="3" type="stmt" count="2" />
      <line num="4" type="cond" truecount="1" falsecount="0" />
      <line num="4" type="cond" truecount="1" falsecount="1" />
      <line num="5" type="stmt" count="0" />
      <line num="5" type="cond" truecount="1" falsecount="0" />
      <line num="6" type="stmt" count="3" />
      <line num="6" type="cond" truecount="1" falsecount="0" />
      """);

    lines.ShouldBe(
    [
      new LineCoverage(3, 2, 0, 0, true, null),
      new LineCoverage(4, 0, 2, 2, false, null),
      new LineCoverage(5, 0, 1, 2, false, null),
      new LineCoverage(6, 3, 2, 2, false, null),
    ]);
  }

  [Fact]
  public async Task Parse_SamePathTwice_ShouldMergeTheirLines()
  {
    var report = await ParseAsync(
      """
      <coverage clover="3.2.0"><project>
        <package><file path="a.js"><line num="1" type="stmt" count="0" /></file></package>
        <package><file path="a.js"><line num="1" type="stmt" count="4" /><line num="2" type="stmt" count="1" /></file></package>
      </project></coverage>
      """);

    report.Files.ShouldHaveSingleItem().Lines.Select(line => (line.Line, line.Hits)).ShouldBe([(1, 4L), (2, 1L)]);
  }

  [Fact]
  public async Task Parse_FilesOutsideAndInsidePackages_ShouldReadThemAll()
  {
    var report = await ParseAsync(
      """
      <coverage clover="3.2.0">
        <project>
          <file name="b.js"><line num="1" type="stmt" count="1" /></file>
          <package><file name="a.js"><line num="1" type="stmt" count="1" /></file></package>
        </project>
        <testproject><file name="c.js"><line num="1" type="stmt" count="1" /></file></testproject>
      </coverage>
      """);

    report.Files.Select(file => file.RawPath).ShouldBe(["a.js", "b.js", "c.js"]);
  }

  [Theory]
  [InlineData("""path="/src/a.js" name="a.js" """, "/src/a.js")]
  [InlineData("""path="" name="a.js" """, "a.js")]
  [InlineData("""name="a.js" """, "a.js")]
  [InlineData("""path="/src/a.js" """, "/src/a.js")]
  public async Task Parse_FileAttributes_ShouldPreferPathThenName(string attributes, string expected)
  {
    var report = await ParseAsync($"""<coverage clover="3.2.0"><file {attributes}><line num="1" type="stmt" count="1" /></file></coverage>""");

    report.Files.ShouldHaveSingleItem().RawPath.ShouldBe(expected);
  }

  [Fact]
  public async Task Parse_FileWithoutName_ShouldThrowFormatException()
  {
    await Should.ThrowAsync<FormatException>(() => ParseAsync("""<coverage clover="3.2.0"><file /></coverage>"""));
  }

  [Theory]
  [InlineData("/${1}.php")]
  [InlineData("{{ template }}/a.js")]
  [InlineData("vendor/lib/a.php")]
  [InlineData("/app/vendor/lib/a.php")]
  public async Task Parse_IgnoredPath_ShouldNotEmitAFile(string path)
  {
    var report = await ParseAsync($"""<coverage clover="3.2.0"><file name="{path}"><line num="1" /></file></coverage>""");

    report.Files.ShouldBeEmpty();
  }

  [Theory]
  [InlineData("vendor/lib/a.js")]
  [InlineData("my-vendor/a.php")]
  public async Task Parse_VendorLookalike_ShouldEmitAFile(string path)
  {
    var report = await ParseAsync($"""<coverage clover="3.2.0"><file name="{path}"><line num="1" type="stmt" count="1" /></file></coverage>""");

    report.Files.ShouldHaveSingleItem().RawPath.ShouldBe(path);
  }

  [Theory]
  [InlineData("""<file name="a.js" />""")]
  [InlineData("""<file name="a.js"></file>""")]
  [InlineData("""<file name="a.js"><metrics loc="1" /></file>""")]
  [InlineData("""<file name="a.js"><class><line /></class></file>""")]
  [InlineData("""<file name=""><line num="1" type="stmt" count="1" /></file>""")]
  public async Task Parse_FileWithoutDirectLine_ShouldNotEmitAFile(string file)
  {
    var report = await ParseAsync($"""<coverage clover="3.2.0">{file}</coverage>""");

    report.Files.ShouldBeEmpty();
  }

  [Fact]
  public async Task Parse_NestedLine_ShouldCountOnceTheFileHasADirectOne()
  {
    var lines = await ParseLinesAsync("""<class><line num="2" type="stmt" count="1" /></class><line num="1" type="stmt" count="1" />""");

    lines.Select(line => line.Line).ShouldBe([1, 2]);
  }

  [Fact]
  public async Task Parse_FileWhoseLinesAreAllSkipped_ShouldNotEmitAFile()
  {
    var report = await ParseAsync("""<coverage clover="3.2.0"><file name="a.js"><line num="0" /></file></coverage>""");

    report.Files.ShouldBeEmpty();
  }

  [Fact]
  public async Task Parse_PhpLineBeyondLoc_ShouldBeSkippedEvenInvalid()
  {
    var lines = await ParsePhpLinesAsync(
      """
      <class><metrics statements="6" /></class>
      <line num="86" type="stmt" count="1" />
      <line num="87" type="stmt" count="1" />
      <line num="88" />
      <metrics loc="86" />
      """);

    lines.Select(line => line.Line).ShouldBe([86]);
  }

  [Theory]
  [InlineData("""<metrics loc="0" />""")]
  [InlineData("""<metrics loc="x" />""")]
  [InlineData("""<metrics statements="6" />""")]
  [InlineData("")]
  [InlineData("""<metrics loc="0" /><metrics loc="1" />""")]
  public async Task Parse_PhpWithoutUsableLoc_ShouldNotBoundTheLines(string metrics)
  {
    var lines = await ParsePhpLinesAsync($"""<line num="87" type="stmt" count="1" />{metrics}""");

    lines.Select(line => line.Line).ShouldBe([87]);
  }

  [Fact]
  public async Task Parse_PhpInvalidLocThenValid_ShouldUseTheFirstValid()
  {
    var lines = await ParsePhpLinesAsync("""<line num="87" type="stmt" count="1" /><metrics loc="x" /><metrics loc="86" /><metrics loc="90" />""");

    lines.ShouldBeEmpty();
  }

  [Fact]
  public async Task Parse_NotPhpBeyondLoc_ShouldKeepTheLine()
  {
    var lines = await ParseLinesAsync("""<line num="87" type="stmt" count="1" /><metrics loc="86" />""");

    lines.Select(line => line.Line).ShouldBe([87]);
  }

  [Theory]
  [InlineData("""<line type="stmt" count="1" />""")]
  [InlineData("""<line num="x" type="stmt" count="1" />""")]
  [InlineData("""<line num="99999999999" type="stmt" count="1" />""")]
  [InlineData("""<line num="1" count="1" />""")]
  [InlineData("""<line num="1" type="stmt" count="x" />""")]
  [InlineData("""<line num="1" type="stmt" count="1.5" />""")]
  [InlineData("""<line num="1" type="method" count="x" />""")]
  [InlineData("""<line num="1" type="cond" falsecount="1" />""")]
  [InlineData("""<line num="1" type="cond" truecount="1" />""")]
  [InlineData("""<line num="1" type="cond" truecount="" falsecount="1" />""")]
  [InlineData("""<line num="1" type="cond" truecount="1" falsecount="x" />""")]
  public async Task Parse_InvalidLine_ShouldThrowFormatException(string line)
  {
    await Should.ThrowAsync<FormatException>(() => ParseLinesAsync(line));
  }

  [Fact]
  public async Task Parse_InvalidLineOfIgnoredFile_ShouldNotThrow()
  {
    var report = await ParseAsync("""<coverage clover="3.2.0"><file name="vendor/a.php"><line num="x" /></file></coverage>""");

    report.Files.ShouldBeEmpty();
  }

  // The report of test_clover.py in codecov/umbrella, with the totals it expects.
  [Fact]
  public async Task Parse_CodecovSample_ShouldMatchCodecovTotals()
  {
    var report = await ParseAsync(
      """
      <coverage generated="1">
        <project timestamp="1410539625">
          <package name="Codecov">
            <file name="source.php">
              <class name="Coverage" namespace="Codecov"><metrics methods="1" statements="6" /></class>
              <line num="5" type="method" name="send" crap="125.96" count="1" />
              <line complexity="9" visibility="private" num="6" count="2969" type="method" />
              <line falsecount="0" truecount="1" num="1" type="cond" />
              <line falsecount="1" truecount="0" num="2" type="cond" />
              <line falsecount="1" truecount="1" num="3" type="cond" />
              <line falsecount="0" truecount="0" num="4" type="cond" />
              <line num="8" type="stmt" count="0" />
              <line num="11" type="stmt" count="1" />
              <line num="21" type="stmt" count="0" />
              <line num="22" type="stmt" count="0" />
              <line num="23" type="stmt" count="0" />
              <line num="87" type="stmt" count="0" />
              <metrics loc="86" ncloc="62" classes="1" methods="1" />
            </file>
            <file path="file.php">
              <class name="Coverage" namespace="Codecov"><metrics methods="1" /></class>
              <line num="11" type="stmt" count="1" />
            </file>
            <file name="nolines"><class name="Coverage" namespace="Codecov"><metrics methods="1" /></class></file>
            <file name="vendor/ignoreme.php"><class name="Coverage" namespace="Codecov"><metrics methods="1" /></class></file>
            <file name="/${1}.php"><class name="Coverage" namespace="Codecov"><metrics methods="1" /></class></file>
          </package>
          <metrics files="1" loc="86" ncloc="62" classes="1" methods="1" />
        </project>
      </coverage>
      """);

    var source = report.Files.Single(file => file.RawPath == "source.php");

    report.Files.Select(file => file.RawPath).ShouldBe(["file.php", "source.php"]);
    source.Lines.Select(line => line.Line).ShouldBe([1, 2, 3, 4, 5, 6, 8, 11, 21, 22, 23]);
    TotalsOf(report).ShouldBe(new CoverageTotals(5, 5, 2, 4, 2));
  }

  [Fact]
  public async Task Parse_IstanbulFixture_ShouldMatchCodecovTotals()
  {
    var report = await ParseFixtureAsync();

    report.Format.ShouldBe(ReportFormat.Clover);
    report.SourceRoots.ShouldBeEmpty();
    report.Files.Select(file => file.RawPath).ShouldBe(
    [
      "/home/runner/work/sample/sample/src/math.js",
      "/home/runner/work/sample/sample/src/shapes/area.js",
      "/home/runner/work/sample/sample/src/unused.js",
    ]);
    TotalsOf(report).ShouldBe(new CoverageTotals(14, 8, 6, 11, 0));
  }

  // Istanbul writes the covered branches in truecount and the others in falsecount: Codecov's rule
  // reads them as the two sides of one condition, so a full or an empty if is one branch of two.
  [Theory]
  [InlineData(8, 2, 2)]
  [InlineData(15, 3, 1)]
  [InlineData(32, 0, 1)]
  public async Task Parse_IstanbulConditions_ShouldFollowCodecovRule(int number, long hits, int covered)
  {
    var report = await ParseFixtureAsync();

    var math = report.Files.Single(file => file.RawPath.EndsWith("/math.js", StringComparison.Ordinal));

    math.Lines.Single(line => line.Line == number).ShouldBe(new LineCoverage(number, hits, covered, 2, false, null));
  }

  [Fact]
  public async Task Parse_MinifiedReport_ShouldReadEachFileAndLine()
  {
    var report = await ParseAsync(
      """<coverage clover="3.2.0"><file name="a.js"><line num="1" type="stmt" count="1"/><line num="2" type="stmt" count="1"/></file><file name="b.js"><line num="1" type="stmt" count="1"/></file></coverage>""");

    report.Files.Select(file => (file.RawPath, file.Lines.Count)).ShouldBe([("a.js", 2), ("b.js", 1)]);
  }

  [Fact]
  public async Task Parse_XxePayload_ShouldThrowXmlException()
  {
    const string Xxe =
      """
      <?xml version="1.0"?>
      <!DOCTYPE coverage SYSTEM "http://xxe.invalid/evil.dtd" [ <!ENTITY xxe SYSTEM "file:///etc/passwd"> ]>
      <coverage clover="3.2.0"><file name="&xxe;"><line num="1" type="stmt" count="1" /></file></coverage>
      """;

    await Should.ThrowAsync<XmlException>(() => ParseAsync(Xxe));
  }

  [Theory]
  [InlineData("")]
  [InlineData("<coverage>")]
  [InlineData("""<coverage><file name="a.js"><line num="1" type="stmt" count="1">""")]
  public async Task Parse_MalformedXml_ShouldThrowXmlException(string xml)
  {
    await Should.ThrowAsync<XmlException>(() => ParseAsync(xml));
  }

  [Fact]
  public async Task Parse_CancelledToken_ShouldThrowOperationCanceledException()
  {
    using var report = new MemoryStream(Encoding.UTF8.GetBytes("""<coverage clover="3.2.0" />"""));

    await Should.ThrowAsync<OperationCanceledException>(() => _parser.ParseAsync(report, new CancellationToken(canceled: true)));
  }

  [Fact]
  public async Task Parse_NullReport_ShouldThrowArgumentNullException()
  {
    await Should.ThrowAsync<ArgumentNullException>(() => _parser.ParseAsync(null!, TestContext.Current.CancellationToken));
  }

  [Fact]
  public void Format_ShouldBeClover()
  {
    _parser.Format.ShouldBe(ReportFormat.Clover);
  }

  [Fact]
  public async Task AddInfrastructure_ShouldRegisterTheCloverParser()
  {
    var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>()).Build();

    await using var services = new ServiceCollection()
                               .AddSingleton<IConfiguration>(configuration)
                               .AddSingleton(A.Fake<ISecretProtector>())
                               .AddLogging()
                               .AddInfrastructure()
                               .BuildServiceProvider();

    services.GetRequiredService<IReportParserCatalog>().Find(ReportFormat.Clover).ShouldBeOfType<CloverParser>();
  }

  private static CoverageTotals TotalsOf(ParsedReport report) =>
    CoverageReport.Create(report.Files.Select(file => FileCoverage.Create(file.RawPath, file.Lines))).Totals;

  private async Task<IReadOnlyList<LineCoverage>> ParseLinesAsync(string content)
  {
    var report = await ParseAsync($"""<coverage clover="3.2.0"><project><file name="{FileName}">{content}</file></project></coverage>""");

    return report.Files.Count == 0 ? [] : report.Files.ShouldHaveSingleItem().Lines;
  }

  private async Task<IReadOnlyList<LineCoverage>> ParsePhpLinesAsync(string content)
  {
    var report = await ParseAsync($"""<coverage clover="3.2.0"><project><file name="a.php">{content}</file></project></coverage>""");

    return report.Files.Count == 0 ? [] : report.Files.ShouldHaveSingleItem().Lines;
  }

  private async Task<ParsedReport> ParseAsync(string xml)
  {
    using var report = new MemoryStream(Encoding.UTF8.GetBytes(xml));

    return await _parser.ParseAsync(report, TestContext.Current.CancellationToken);
  }

  private async Task<ParsedReport> ParseFixtureAsync()
  {
    var path = Path.Combine(AppContext.BaseDirectory, "fixtures", "reports", "clover", "istanbul.xml");

    await using var report = File.OpenRead(path);

    return await _parser.ParseAsync(report, TestContext.Current.CancellationToken);
  }

  #endregion
}
