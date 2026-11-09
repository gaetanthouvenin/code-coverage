// ------------------------------------------------------------------------------------------------
// <copyright file="JacocoParserTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Xml;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using CodeCoverage.Domain.Coverage;
using CodeCoverage.Domain.Paths;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Infrastructure.Reports.Parsers;

namespace CodeCoverage.Infrastructure.Tests.Reports.Parsers;

public sealed class JacocoParserTests
{
  #region Fields

  // The fixture is the real output of JaCoCo 0.8.13 on a small Java sample; only the host name of
  // its sessioninfo was rewritten. The XML of the other tests is hand-written. The expected values
  // are those of Codecov's rules (jacoco.py of codecov/umbrella), run on the fixture. CodecovSample
  // is the report of Codecov's own unit test, test_jacoco.py.
  private const string CodecovSample =
    """
    <?xml version="1.0" encoding="UTF-8" standalone="yes" ?>
    <!DOCTYPE report PUBLIC "-//JACOCO//DTD Report 1.0//EN" "report.dtd">
    <report name="JaCoCo Maven plug-in example for Java project">
      <sessioninfo id="host-b048b758" start="1411925087600" dump="1411925088117" />
      <package name="base">
        <class name="base/source">
          <method name="&lt;init&gt;" line="1">
            <counter type="INSTRUCTION" missed="54" covered="0" />
            <counter type="COMPLEXITY" missed="3" covered="1" />
          </method>
          <method name="ignore"></method>
          <method name="ignore$" line="2">
            <counter type="BRANCH" missed="3" covered="3" />
            <counter type="COMPLEXITY" missed="3" covered="1" />
          </method>
        </class>
        <sourcefile name="source.java">
          <line nr="0" mi="0" ci="20" mb="0" cb="0" />
          <line nr="1" mi="99" ci="99" mb="0" cb="2" />
          <line nr="2" mi="0" ci="2" mb="1" cb="1" />
          <line nr="3" mi="1" ci="0" mb="0" cb="0" />
          <line nr="4" mi="0" ci="2" mb="0" cb="0" />
        </sourcefile>
        <sourcefile name="file.java">
          <line nr="1" mi="0" ci="1" mb="0" cb="0" />
        </sourcefile>
        <sourcefile name="empty">
        </sourcefile>
      </package>
    </report>
    """;

  private const string Method = """<method name="m" desc="()V" line="1"><counter type="COMPLEXITY" missed="0" covered="1" /></method>""";

  private const string SourceLine = """<sourcefile name="A.java"><line nr="1" mi="0" ci="1" mb="0" cb="0" /></sourcefile>""";

  private readonly JacocoParser _parser = new();

  #endregion

  #region Methods

  [Theory]
  [InlineData(1, 1, 1, 2)]
  [InlineData(3, 1, 1, 4)]
  [InlineData(2, 0, 0, 2)]
  public async Task Parse_PartialBranches_ShouldEmitCoveredOverTotal(int missed, int covered, int expectedCovered, int expectedTotal)
  {
    var lines = await ParseLinesAsync($"""<line nr="7" mi="1" ci="4" mb="{missed}" cb="{covered}" />""");

    lines.ShouldBe([new LineCoverage(7, 4, expectedCovered, expectedTotal, false, null)]);
  }

  [Fact]
  public async Task Parse_FullyCoveredBranches_ShouldEmitCoveredOverCovered()
  {
    var lines = await ParseLinesAsync("""<line nr="7" mi="9" ci="0" mb="0" cb="2" />""");

    lines.ShouldBe([new LineCoverage(7, 0, 2, 2, false, null)]);
  }

  [Theory]
  [InlineData(0, 2, 2)]
  [InlineData(5, 0, 0)]
  public async Task Parse_NoBranch_ShouldUseCoveredInstructions(int missedInstructions, int coveredInstructions, long expected)
  {
    var lines = await ParseLinesAsync($"""<line nr="7" mi="{missedInstructions}" ci="{coveredInstructions}" mb="0" cb="0" />""");

    lines.ShouldBe([new LineCoverage(7, expected, 0, 0, false, null)]);
  }

  // Codecov emits a line whatever its counters: JaCoCo itself never writes one with no instruction.
  [Fact]
  public async Task Parse_EmptyCounters_ShouldEmitMiss()
  {
    var lines = await ParseLinesAsync("""<line nr="7" mi="0" ci="0" mb="0" cb="0" />""");

    lines.ShouldBe([new LineCoverage(7, 0, 0, 0, false, null)]);
  }

  [Fact]
  public async Task Parse_Doctype_ShouldNotFetch()
  {
    using var listener = new TcpListener(IPAddress.Loopback, 0);
    listener.Start();
    var port = ((IPEndPoint)listener.LocalEndpoint).Port;

    var report = await ParseAsync(
      $"""
       <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
       <!DOCTYPE report PUBLIC "-//JACOCO//DTD Report 1.1//EN" "http://127.0.0.1:{port}/report.dtd">
       <report name="sample"><package name="p">{SourceLine}</package></report>
       """);

    report.Files.ShouldHaveSingleItem().RawPath.ShouldBe("p/A.java");
    listener.Pending().ShouldBeFalse();
  }

  [Fact]
  public async Task Parse_WithDoctypeFixture_ShouldMatchCodecovTotals()
  {
    var report = await ParseFixtureAsync();

    var totals = CoverageReport.Create(report.Files.Select(file => FileCoverage.Create(file.RawPath, file.Lines))).Totals;

    report.Format.ShouldBe(ReportFormat.Jacoco);
    report.Files.Select(file => file.RawPath).ShouldBe(["Main.java", "com/example/calc/Calculator.java", "com/example/util/Shouter.java", "com/example/util/Strings.java"]);
    report.SourceRoots.ShouldBe(["sample-core", "sample-core/src/main/java"]);
    totals.ShouldBe(new CoverageTotals(16, 10, 4, 2, 6));
  }

  [Fact]
  public async Task Parse_WithDoctypeFixture_ShouldMatchCodecovLineByLine()
  {
    var report = await ParseFixtureAsync();

    var lines = report.Files.SelectMany(file => file.Lines.Select(line => Describe(file.RawPath, line)));

    lines.ShouldBe(
    [
      "Main.java|4|0|Line",
      "Main.java|6|4|Line",
      "Main.java|8|6|Line",
      "Main.java|9|5|Line",
      "Main.java|10|5|Line",
      "Main.java|11|5|Line",
      "Main.java|12|6|Line",
      "Main.java|13|4|Line",
      "Main.java|15|1/2|Branch",
      "Main.java|16|0|Line",
      "Main.java|18|1|Line",
      "Main.java|21|0|Line",
      "com/example/calc/Calculator.java|3|3|Method",
      "com/example/calc/Calculator.java|5|1/2|Method",
      "com/example/calc/Calculator.java|6|0|Line",
      "com/example/calc/Calculator.java|9|4|Line",
      "com/example/calc/Calculator.java|13|2/2|Method",
      "com/example/calc/Calculator.java|14|2|Line",
      "com/example/calc/Calculator.java|17|1/2|Branch",
      "com/example/calc/Calculator.java|21|2/6|Method",
      "com/example/calc/Calculator.java|25|0/2|Method",
      "com/example/calc/Calculator.java|26|0|Line",
      "com/example/calc/Calculator.java|29|0|Line",
      "com/example/calc/Calculator.java|32|3|Line",
      "com/example/calc/Calculator.java|34|2|Line",
      "com/example/calc/Calculator.java|38|0|Line",
      "com/example/util/Shouter.java|8|3|Method",
      "com/example/util/Strings.java|3|0|Line",
      "com/example/util/Strings.java|5|3|Line",
      "com/example/util/Strings.java|9|0|Line",
    ]);
  }

  [Fact]
  public async Task Parse_CodecovUnitTestReport_ShouldGiveItsExpectedLines()
  {
    var report = await ParseAsync(CodecovSample);

    report.Files.Select(file => file.RawPath).ShouldBe(["base/file.java", "base/source.java"]);
    report.Files[0].Lines.ShouldBe([new LineCoverage(1, 1, 0, 0, false, null)]);
    report.Files[1].Lines.ShouldBe(
    [
      new LineCoverage(1, 99, 2, 2, true, null),
      new LineCoverage(2, 2, 1, 2, true, null),
      new LineCoverage(3, 0, 0, 0, false, null),
      new LineCoverage(4, 2, 0, 0, false, null),
    ]);
  }

  [Fact]
  public async Task Parse_DefaultPackage_ShouldEmitTheFileNameAlone()
  {
    var report = await ParseAsync($"""<report name="a"><package name="">{SourceLine}</package></report>""");

    report.Files.ShouldHaveSingleItem().RawPath.ShouldBe("A.java");
  }

  [Fact]
  public async Task Parse_MethodOfTheClassOfTheFile_ShouldFlagItsLine()
  {
    var report = await ParsePackageAsync($"""<class name="p/A">{Method}</class>{SourceLine}""");

    report.Files.ShouldHaveSingleItem().Lines.ShouldHaveSingleItem().IsMethod.ShouldBeTrue();
  }

  [Fact]
  public async Task Parse_SourceFileBeforeItsClass_ShouldStillFlagTheLine()
  {
    var report = await ParsePackageAsync($"""{SourceLine}<class name="p/A">{Method}</class>""");

    report.Files.ShouldHaveSingleItem().Lines.ShouldHaveSingleItem().IsMethod.ShouldBeTrue();
  }

  [Theory]
  [InlineData("p/A$B")]
  [InlineData("p/AKt")]
  [InlineData("p/Other")]
  public async Task Parse_MethodOfAnotherClass_ShouldNotFlagTheLine(string className)
  {
    var report = await ParsePackageAsync($"""<class name="{className}">{Method}</class>{SourceLine}""");

    report.Files.ShouldHaveSingleItem().Lines.ShouldHaveSingleItem().IsMethod.ShouldBeFalse();
  }

  [Theory]
  [InlineData("""<method name="m" line="1"><counter type="INSTRUCTION" missed="0" covered="1" /></method>""")]
  [InlineData("""<method name="m" line="1" />""")]
  [InlineData("""<method name="m"><counter type="COMPLEXITY" missed="0" covered="1" /></method>""")]
  [InlineData("""<method name="m" line="0"><counter type="COMPLEXITY" missed="x" covered="1" /></method>""")]
  public async Task Parse_MethodThatMarksNoLine_ShouldNotFlagTheLine(string method)
  {
    var report = await ParsePackageAsync($"""<class name="p/A">{method}</class>{SourceLine}""");

    report.Files.ShouldHaveSingleItem().Lines.ShouldHaveSingleItem().IsMethod.ShouldBeFalse();
  }

  [Fact]
  public async Task Parse_MethodInDefaultPackage_ShouldNotFlagTheLine()
  {
    var report = await ParseAsync($"""<report name="a"><package name=""><class name="A">{Method}</class>{SourceLine}</package></report>""");

    report.Files.ShouldHaveSingleItem().Lines.ShouldHaveSingleItem().IsMethod.ShouldBeFalse();
  }

  [Fact]
  public async Task Parse_MethodOnBranchLine_ShouldKeepTheBranchesAndCountAsMethod()
  {
    var report = await ParsePackageAsync(
      $"""<class name="p/A">{Method}</class><sourcefile name="A.java"><line nr="1" mi="0" ci="2" mb="1" cb="1" /></sourcefile>""");

    var line = report.Files.ShouldHaveSingleItem().Lines.ShouldHaveSingleItem();

    line.ShouldBe(new LineCoverage(1, 2, 1, 2, true, null));
    line.Kind.ShouldBe(LineKind.Method);
  }

  [Fact]
  public async Task Parse_AggregateCounters_ShouldBeIgnored()
  {
    var lines = await ParseLinesAsync("""<line nr="1" mi="0" ci="1" mb="0" cb="0" /><counter type="LINE" missed="99" covered="0" />""");

    lines.ShouldBe([new LineCoverage(1, 1, 0, 0, false, null)]);
  }

  [Fact]
  public async Task Parse_SameFileInSeveralPackages_ShouldMergeTheLines()
  {
    var report = await ParseAsync(
      """
      <report name="a">
        <group name="x"><package name="p"><sourcefile name="A.java"><line nr="1" mi="1" ci="0" mb="0" cb="0" /><line nr="2" mi="0" ci="1" mb="0" cb="0" /></sourcefile></package></group>
        <group name="y"><package name="p"><sourcefile name="A.java"><line nr="1" mi="0" ci="3" mb="0" cb="0" /></sourcefile></package></group>
      </report>
      """);

    report.Files.ShouldHaveSingleItem().Lines.ShouldBe(
    [
      new LineCoverage(1, 3, 0, 0, false, null),
      new LineCoverage(2, 1, 0, 0, false, null),
    ]);
  }

  [Fact]
  public async Task Parse_MinifiedReport_ShouldReadEachLine()
  {
    var lines = await ParseLinesAsync("""<line nr="1" mi="0" ci="1" mb="0" cb="0"/><line nr="2" mi="0" ci="1" mb="0" cb="0"/>""");

    lines.Select(line => line.Line).ShouldBe([1, 2]);
  }

  [Theory]
  [InlineData("sample-core", new[] { "sample-core", "sample-core/src/main/java" })]
  [InlineData("/sample-core/", new[] { "sample-core", "sample-core/src/main/java" })]
  public async Task Parse_ReportName_ShouldGiveTheModuleDirectories(string name, string[] expected)
  {
    var report = await ParseAsync($"""<report name="{name}" />""");

    report.SourceRoots.ShouldBe(expected);
  }

  [Theory]
  [InlineData("""<report name="My Maven Project" />""")]
  [InlineData("""<report name="" />""")]
  [InlineData("""<report name="/" />""")]
  [InlineData("""<report />""")]
  public async Task Parse_ReportNameThatIsNoDirectory_ShouldGiveNoSourceRoot(string xml)
  {
    var report = await ParseAsync(xml);

    report.SourceRoots.ShouldBeEmpty();
  }

  // The acceptance of M1.13: JaCoCo paths start at the package, so only their suffix is in the repository.
  [Theory]
  [InlineData("com/example/calc/Calculator.java", "services/billing/src/main/java/com/example/calc/Calculator.java")]
  [InlineData("com/example/util/Strings.java", "services/billing/src/main/java/com/example/util/Strings.java")]
  public async Task Fix_PackagePath_ShouldResolveBySuffix(string rawPath, string expected)
  {
    var report = await ParseFixtureAsync();
    var fixer = new PathFixer(
      new PathFixContext
      {
        SourceRoots = report.SourceRoots,
        Network = new NetworkIndex(["services/billing/src/main/java/com/example/calc/Calculator.java", "services/billing/src/main/java/com/example/util/Strings.java", "README.md"]),
      });

    report.Files.ShouldContain(file => file.RawPath == rawPath);

    fixer.Fix(rawPath).ShouldBe(PathFixResult.Resolved(expected));
  }

  // Codecov's test_multi_module: the name of the report tells which module the package belongs to.
  [Theory]
  [InlineData("module_a", "module_a/src/main/java/p/A.java")]
  [InlineData("module_b", "module_b/src/main/java/p/A.java")]
  public async Task Fix_SamePackageInSeveralModules_ShouldTakeTheModuleOfTheReport(string reportName, string expected)
  {
    var report = await ParseAsync($"""<report name="{reportName}"><package name="p">{SourceLine}</package></report>""");
    var fixer = new PathFixer(
      new PathFixContext
      {
        SourceRoots = report.SourceRoots,
        Network = new NetworkIndex(["module_a/src/main/java/p/A.java", "module_b/src/main/java/p/A.java"]),
      });

    fixer.Fix(report.Files.ShouldHaveSingleItem().RawPath).ShouldBe(PathFixResult.Resolved(expected));
  }

  [Fact]
  public async Task Fix_ModuleWithoutSourceDirectory_ShouldTakeTheProjectPrefix()
  {
    var report = await ParseAsync($"""<report name="module_a"><package name="p">{SourceLine}</package></report>""");
    var fixer = new PathFixer(
      new PathFixContext
      {
        SourceRoots = report.SourceRoots,
        Network = new NetworkIndex(["module_a/p/A.java", "module_b/p/A.java"]),
      });

    fixer.Fix("p/A.java").ShouldBe(PathFixResult.Resolved("module_a/p/A.java"));
  }

  [Theory]
  [InlineData("0")]
  [InlineData("-3")]
  public async Task Parse_LineNumberNotCoverable_ShouldSkipTheLine(string number)
  {
    var lines = await ParseLinesAsync($"""<line nr="{number}" mi="0" ci="1" mb="0" cb="0" /><line nr="9" mi="0" ci="1" mb="0" cb="0" />""");

    lines.ShouldBe([new LineCoverage(9, 1, 0, 0, false, null)]);
  }

  [Fact]
  public async Task Parse_NegativeInstructions_ShouldBeBoundedToZero()
  {
    var lines = await ParseLinesAsync("""<line nr="1" mi="0" ci="-4" mb="0" cb="0" />""");

    lines.ShouldBe([new LineCoverage(1, 0, 0, 0, false, null)]);
  }

  [Theory]
  [InlineData("""<line nr="1" mi="0" mb="1" cb="1" />""", 0)]
  [InlineData("""<line nr="1" mi="0" ci="x" mb="0" cb="2" />""", 0)]
  public async Task Parse_BranchLineWithoutValidInstructions_ShouldKeepTheBranchData(string line, long expected)
  {
    var lines = await ParseLinesAsync(line);

    lines.ShouldHaveSingleItem().Hits.ShouldBe(expected);
    lines[0].IsBranch.ShouldBeTrue();
  }

  [Theory]
  [InlineData("""<line mi="0" ci="1" mb="0" cb="0" />""")]
  [InlineData("""<line nr="x" mi="0" ci="1" mb="0" cb="0" />""")]
  [InlineData("""<line nr="1.0" mi="0" ci="1" mb="0" cb="0" />""")]
  [InlineData("""<line nr="1" mi="0" ci="1" cb="0" />""")]
  [InlineData("""<line nr="1" mi="0" ci="1" mb="0" />""")]
  [InlineData("""<line nr="1" mi="0" ci="1" mb="x" cb="0" />""")]
  [InlineData("""<line nr="1" mi="0" ci="1" mb="0" cb="x" />""")]
  [InlineData("""<line nr="1" mi="0" ci="1" mb="-1" cb="2" />""")]
  [InlineData("""<line nr="1" mi="0" ci="1" mb="2147483647" cb="1" />""")]
  [InlineData("""<line nr="1" mi="0" mb="0" cb="0" />""")]
  [InlineData("""<line nr="1" mi="0" ci="x" mb="0" cb="0" />""")]
  public async Task Parse_InvalidLine_ShouldThrowFormatException(string line)
  {
    await Should.ThrowAsync<FormatException>(() => ParseLinesAsync(line));
  }

  [Theory]
  [InlineData("""<package />""")]
  [InlineData("""<package name="p"><sourcefile /></package>""")]
  [InlineData("""<package name="p"><class /></package>""")]
  [InlineData("""<package name="p"><class name="p/A"><method line="x" /></class></package>""")]
  [InlineData("""<package name="p"><class name="p/A"><method line="1"><counter /></method></class></package>""")]
  [InlineData("""<package name="p"><class name="p/A"><method line="1"><counter type="COMPLEXITY" covered="1" /></method></class></package>""")]
  [InlineData("""<package name="p"><class name="p/A"><method line="1"><counter type="COMPLEXITY" missed="x" covered="1" /></method></class></package>""")]
  public async Task Parse_InvalidStructure_ShouldThrowFormatException(string package)
  {
    await Should.ThrowAsync<FormatException>(() => ParseAsync($"""<report name="a">{package}</report>"""));
  }

  [Theory]
  [InlineData("""<package name="p" />""")]
  [InlineData("""<package name="p"></package>""")]
  [InlineData("""<package name="p"><sourcefile name="A.java" /></package>""")]
  [InlineData("""<package name="p"><sourcefile name="A.java"><counter type="LINE" missed="0" covered="1" /></sourcefile></package>""")]
  public async Task Parse_NoLine_ShouldNotEmitAFile(string package)
  {
    var report = await ParseAsync($"""<report name="a">{package}</report>""");

    report.Files.ShouldBeEmpty();
  }

  [Fact]
  public async Task Parse_EmptyClass_ShouldNotConsumeTheNextElement()
  {
    var report = await ParsePackageAsync($"""<class name="p/A" />{SourceLine}""");

    report.Files.ShouldHaveSingleItem().RawPath.ShouldBe("p/A.java");
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
  [InlineData("<report>")]
  public async Task Parse_MalformedXml_ShouldThrowXmlException(string xml)
  {
    await Should.ThrowAsync<XmlException>(() => ParseAsync(xml));
  }

  [Fact]
  public async Task Parse_CancelledToken_ShouldThrowOperationCanceledException()
  {
    using var report = new MemoryStream(Encoding.UTF8.GetBytes("""<report name="a" />"""));

    await Should.ThrowAsync<OperationCanceledException>(() => _parser.ParseAsync(report, new CancellationToken(canceled: true)));
  }

  [Fact]
  public async Task Parse_NullReport_ShouldThrowArgumentNullException()
  {
    await Should.ThrowAsync<ArgumentNullException>(() => _parser.ParseAsync(null!, TestContext.Current.CancellationToken));
  }

  [Fact]
  public void Format_ShouldBeJacoco()
  {
    _parser.Format.ShouldBe(ReportFormat.Jacoco);
  }

  [Fact]
  public async Task AddInfrastructure_ShouldRegisterTheJacocoParser()
  {
    var configuration = new ConfigurationBuilder().AddInMemoryCollection([]).Build();

    await using var services = new ServiceCollection()
                               .AddSingleton<IConfiguration>(configuration)
                               .AddSingleton(A.Fake<ISecretProtector>())
                               .AddLogging()
                               .AddInfrastructure()
                               .BuildServiceProvider();

    services.GetRequiredService<IReportParserCatalog>().Find(ReportFormat.Jacoco).ShouldBeOfType<JacocoParser>();
  }

  private static string Describe(string path, LineCoverage line) =>
    $"{path}|{line.Line}|{(line.IsBranch ? $"{line.Covered}/{line.Total}" : line.Hits.ToString(CultureInfo.InvariantCulture))}|{line.Kind}";

  private async Task<IReadOnlyList<LineCoverage>> ParseLinesAsync(string content)
  {
    var report = await ParsePackageAsync($"""<sourcefile name="A.java">{content}</sourcefile>""");

    return report.Files.Count == 0 ? [] : report.Files.ShouldHaveSingleItem().Lines;
  }

  private Task<ParsedReport> ParsePackageAsync(string content) => ParseAsync($"""<report name="a"><package name="p">{content}</package></report>""");

  private async Task<ParsedReport> ParseAsync(string xml)
  {
    using var report = new MemoryStream(Encoding.UTF8.GetBytes(xml));

    return await _parser.ParseAsync(report, TestContext.Current.CancellationToken);
  }

  private async Task<ParsedReport> ParseFixtureAsync()
  {
    var path = Path.Combine(AppContext.BaseDirectory, "fixtures", "reports", "jacoco", "with-doctype.xml");

    await using var report = File.OpenRead(path);

    return await _parser.ParseAsync(report, TestContext.Current.CancellationToken);
  }

  #endregion
}
