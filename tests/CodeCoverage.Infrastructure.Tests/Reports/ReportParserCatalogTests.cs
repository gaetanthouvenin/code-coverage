// ------------------------------------------------------------------------------------------------
// <copyright file="ReportParserCatalogTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Text;

using CodeCoverage.Domain.Coverage;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Infrastructure.Reports;

namespace CodeCoverage.Infrastructure.Tests.Reports;

public sealed class ReportParserCatalogTests
{
  #region Fields

  private const string CoberturaHead = """<?xml version="1.0"?><coverage line-rate="1" branch-rate="1"><packages /></coverage>""";
  private const string AmbiguousCoverage = """<?xml version="1.0"?><coverage version="1"><sources />""";
  private const string LcovRecord = "TN:\nSF:/src/a.ts\nDA:1,1\nend_of_record\n";

  // The parser fixtures land in these folders, with the micro-feature of their format.
  private static readonly Dictionary<string, ReportFormat> FixtureFolders = new()
  {
    ["cobertura"] = ReportFormat.Cobertura,
    ["opencover"] = ReportFormat.OpenCover,
    ["lcov"] = ReportFormat.Lcov,
    ["jacoco"] = ReportFormat.Jacoco,
    ["clover"] = ReportFormat.Clover,
    ["security"] = ReportFormat.Cobertura,
  };

  private readonly ReportParserCatalog _catalog = new([]);

  #endregion

  #region Properties

  public static TheoryData<string, string, ReportFormat> Fixtures
  {
    get
    {
      var data = new TheoryData<string, string, ReportFormat>
      {
        { "coverage.opencover.xml", """<?xml version="1.0"?><CoverageSession xmlns:xsd="x"><Summary /></CoverageSession>""", ReportFormat.OpenCover },
        { "jacoco.xml", """<?xml version="1.0"?><!DOCTYPE report PUBLIC "-//JACOCO//DTD Report 1.1//EN" "report.dtd"><report name="a" />""", ReportFormat.Jacoco },
        { "clover.xml", """<?xml version="1.0"?><coverage generated="1" clover="3.2.0"></coverage>""", ReportFormat.Clover },
        { "coverage.xml", """<?xml version="1.0"?><coverage generated="1"><project timestamp="1" /></coverage>""", ReportFormat.Clover },
        { "coverage.xml", CoberturaHead, ReportFormat.Cobertura },
        { "coverage.xml", """<coverage lines-valid="3"></coverage>""", ReportFormat.Cobertura },
        { "coverage.xml", """<coverage><sources /><packages /></coverage>""", ReportFormat.Cobertura },
        { "lcov.info", LcovRecord, ReportFormat.Lcov },
      };

      foreach (var (folder, format) in FixtureFolders)
      {
        var directory = Path.Combine(AppContext.BaseDirectory, "fixtures", "reports", folder);

        foreach (var file in Directory.Exists(directory) ? Directory.EnumerateFiles(directory) : [])
        {
          data.Add(Path.GetFileName(file), File.ReadAllText(file), format);
        }
      }

      return data;
    }
  }

  #endregion

  #region Methods

  [Theory]
  [MemberData(nameof(Fixtures))]
  public void Detect_EachFixture_ShouldReturnExpectedFormat(string fileName, string content, ReportFormat expected)
  {
    _catalog.Detect(Encoding.UTF8.GetBytes(content), fileName).ShouldBe(expected);
  }

  [Fact]
  public void Detect_CoverletJson_ShouldReturnNull()
  {
    const string Json = """{ "Sample.dll": { "/src/Sample/Calculator.cs": { "Sample.Calculator": {} } } }""";

    _catalog.Detect(Encoding.UTF8.GetBytes(Json), "coverage.json").ShouldBeNull();
  }

  [Theory]
  [InlineData("utf-8")]
  [InlineData("utf-16")]
  [InlineData("utf-16BE")]
  public void Detect_ReportWithByteOrderMark_ShouldDecodeIt(string encodingName)
  {
    var encoding = Encoding.GetEncoding(encodingName);
    byte[] report = [.. encoding.GetPreamble(), .. encoding.GetBytes(LcovRecord)];

    _catalog.Detect(report, "report.txt").ShouldBe(ReportFormat.Lcov);
  }

  [Fact]
  public void Detect_Utf16Declaration_ShouldReadTheRootElement()
  {
    var report = Encoding.Unicode.GetPreamble()
                         .Concat(Encoding.Unicode.GetBytes("""<?xml version="1.0" encoding="utf-16"?><report name="a" />"""))
                         .ToArray();

    _catalog.Detect(report, "report.xml").ShouldBe(ReportFormat.Jacoco);
  }

  [Fact]
  public void Detect_MarkupInComment_ShouldLookAtTheRootElement()
  {
    var report = "<!-- <report> <CoverageSession> -->" + CoberturaHead.Replace("""<?xml version="1.0"?>""", string.Empty, StringComparison.Ordinal);

    _catalog.Detect(Encoding.UTF8.GetBytes(report), "report.xml").ShouldBe(ReportFormat.Cobertura);
  }

  [Fact]
  public void Detect_HintBeyondTheWindow_ShouldNotSeeIt()
  {
    var report = LcovRecord.Replace("end_of_record", new string('x', IReportParserCatalog.HeadLength) + "end_of_record", StringComparison.Ordinal);

    _catalog.Detect(Encoding.UTF8.GetBytes(report), "report.txt").ShouldBeNull();
  }

  [Theory]
  [InlineData("lcov.info")]
  [InlineData("coverage.lcov")]
  [InlineData("sub/dir/LCOV-unit.txt")]
  public void Detect_FirstRecordOutgrowingTheWindow_ShouldTrustATracefileName(string fileName)
  {
    var report = "SF:/src/a.ts\n" + string.Concat(Enumerable.Range(1, 2000).Select(line => $"DA:{line},1\n")) + "end_of_record\n";

    _catalog.Detect(Encoding.UTF8.GetBytes(report), fileName).ShouldBe(ReportFormat.Lcov);
  }

  [Theory]
  [InlineData("clover.xml", ReportFormat.Clover)]
  [InlineData("coverage.cobertura.xml", ReportFormat.Cobertura)]
  [InlineData(@"C:\agent\clover\coverage.xml", null)]
  [InlineData("coverage.xml", null)]
  public void Detect_AmbiguousCoverageRoot_ShouldFallBackOnTheName(string fileName, ReportFormat? expected)
  {
    _catalog.Detect(Encoding.UTF8.GetBytes(AmbiguousCoverage), fileName).ShouldBe(expected);
  }

  [Theory]
  [InlineData("<coverage line-rate=", "cobertura.xml")]
  [InlineData("<html><body /></html>", "cobertura.xml")]
  [InlineData("not a report", "lcov.info")]
  [InlineData("", "lcov.info")]
  public void Detect_ContentThatIsNoReport_ShouldReturnNullWhateverTheName(string content, string fileName)
  {
    _catalog.Detect(Encoding.UTF8.GetBytes(content), fileName).ShouldBeNull();
  }

  [Fact]
  public void Detect_NullFileName_ShouldThrowArgumentNullException()
  {
    Should.Throw<ArgumentNullException>(() => _catalog.Detect(ReadOnlyMemory<byte>.Empty, null!));
  }

  [Fact]
  public void Find_RegisteredFormat_ShouldReturnItsParser()
  {
    var parser = A.Fake<IReportParser>();
    A.CallTo(() => parser.Format).Returns(ReportFormat.Lcov);

    new ReportParserCatalog([parser]).Find(ReportFormat.Lcov).ShouldBeSameAs(parser);
  }

  [Fact]
  public void Find_UnregisteredFormat_ShouldReturnNull()
  {
    _catalog.Find(ReportFormat.Clover).ShouldBeNull();
  }

  #endregion
}
