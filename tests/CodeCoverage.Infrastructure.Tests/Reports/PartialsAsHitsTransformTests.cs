// ------------------------------------------------------------------------------------------------
// <copyright file="PartialsAsHitsTransformTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Config;
using CodeCoverage.Domain.Coverage;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Infrastructure.Reports.Parsers;

namespace CodeCoverage.Infrastructure.Tests.Reports;

public sealed class PartialsAsHitsTransformTests
{
  #region Fields

  // The rules are those of cobertura.py, lcov.py and jacoco.py of codecov/umbrella, each read on its own.
  private const int Line = 7;

  private static readonly string[] Missing = ["0:1"];

  #endregion

  #region Methods

  [Fact]
  public void Apply_WhenEnabled_ShouldTurnPartialsIntoHits()
  {
    var report = Report(ReportFormat.Lcov, new LineCoverage(Line, 0, 1, 2, false, Missing));

    var result = PartialsAsHitsTransform.Apply(report, Enabled("lcov"));

    CoverageMath.Classify(LineOf(result)).ShouldBe(LineState.Hit);
  }

  [Fact]
  public void Apply_CoberturaPartial_ShouldBecomeAPlainLine()
  {
    var report = Report(ReportFormat.Cobertura, new LineCoverage(Line, 4, 1, 2, false, Missing));

    var result = PartialsAsHitsTransform.Apply(report, Enabled("cobertura"));

    LineOf(result).ShouldBe(new LineCoverage(Line, 1, 0, 0, false, null));
  }

  [Fact]
  public void Apply_CoberturaFullBranch_ShouldBecomeAPlainLine()
  {
    var report = Report(ReportFormat.Cobertura, new LineCoverage(Line, 4, 2, 2, false, null));

    var result = PartialsAsHitsTransform.Apply(report, Enabled("cobertura"));

    LineOf(result).ShouldBe(new LineCoverage(Line, 1, 0, 0, false, null));
  }

  [Theory]
  [InlineData(ReportFormat.Lcov, "lcov")]
  [InlineData(ReportFormat.Jacoco, "jacoco")]
  public void Apply_BranchPartial_ShouldStayABranchLine(ReportFormat format, string key)
  {
    var report = Report(format, new LineCoverage(Line, 0, 1, 3, false, Missing));

    var result = PartialsAsHitsTransform.Apply(report, Enabled(key));

    LineOf(result).ShouldBe(new LineCoverage(Line, 1, 3, 3, false, null));
    LineOf(result).Kind.ShouldBe(LineKind.Branch);
  }

  [Theory]
  [InlineData(ReportFormat.Lcov, "lcov")]
  [InlineData(ReportFormat.Jacoco, "jacoco")]
  public void Apply_MethodPartial_ShouldStayAMethodLine(ReportFormat format, string key)
  {
    var report = Report(format, new LineCoverage(Line, 0, 1, 2, true, null));

    var result = PartialsAsHitsTransform.Apply(report, Enabled(key));

    LineOf(result).ShouldBe(new LineCoverage(Line, 1, 2, 2, true, null));
    LineOf(result).Kind.ShouldBe(LineKind.Method);
  }

  [Theory]
  [InlineData(ReportFormat.Lcov, "lcov")]
  [InlineData(ReportFormat.Jacoco, "jacoco")]
  public void Apply_BranchFullyCovered_ShouldNotChange(ReportFormat format, string key)
  {
    var line = new LineCoverage(Line, 5, 2, 2, false, null);

    LineOf(PartialsAsHitsTransform.Apply(Report(format, line), Enabled(key))).ShouldBe(line);
  }

  [Theory]
  [InlineData(ReportFormat.Cobertura, "cobertura")]
  [InlineData(ReportFormat.Lcov, "lcov")]
  [InlineData(ReportFormat.Jacoco, "jacoco")]
  public void Apply_NoBranchCovered_ShouldStayAMiss(ReportFormat format, string key)
  {
    var line = new LineCoverage(Line, 0, 0, 2, false, ["0:0", "0:1"]);

    LineOf(PartialsAsHitsTransform.Apply(Report(format, line), Enabled(key))).ShouldBe(line);
  }

  [Theory]
  [InlineData(ReportFormat.Cobertura, "cobertura")]
  [InlineData(ReportFormat.Lcov, "lcov")]
  [InlineData(ReportFormat.Jacoco, "jacoco")]
  public void Apply_PlainLine_ShouldNotChange(ReportFormat format, string key)
  {
    var line = new LineCoverage(Line, 3, 0, 0, false, null);

    LineOf(PartialsAsHitsTransform.Apply(Report(format, line), Enabled(key))).ShouldBe(line);
  }

  [Theory]
  [InlineData(ReportFormat.Cobertura)]
  [InlineData(ReportFormat.Lcov)]
  [InlineData(ReportFormat.Jacoco)]
  public void Apply_WhenDisabled_ShouldReturnTheReport(ReportFormat format)
  {
    var report = Report(format, new LineCoverage(Line, 0, 1, 2, false, Missing));

    PartialsAsHitsTransform.Apply(report, CoverageConfig.Create(CoverageConfigDefaults.Root)).ShouldBeSameAs(report);
  }

  [Theory]
  [InlineData(ReportFormat.Cobertura, "lcov")]
  [InlineData(ReportFormat.Lcov, "jacoco")]
  [InlineData(ReportFormat.Jacoco, "cobertura")]
  public void Apply_OptionOfAnotherFormat_ShouldReturnTheReport(ReportFormat format, string key)
  {
    var report = Report(format, new LineCoverage(Line, 0, 1, 2, false, Missing));

    PartialsAsHitsTransform.Apply(report, Enabled(key)).ShouldBeSameAs(report);
  }

  [Theory]
  [InlineData(ReportFormat.OpenCover)]
  [InlineData(ReportFormat.Clover)]
  public void Apply_FormatWithoutTheOption_ShouldReturnTheReport(ReportFormat format)
  {
    var report = Report(format, new LineCoverage(Line, 0, 1, 2, false, Missing));

    PartialsAsHitsTransform.Apply(report, Enabled("cobertura", "lcov", "jacoco")).ShouldBeSameAs(report);
  }

  [Fact]
  public void Apply_NullReport_ShouldThrow()
  {
    var config = CoverageConfig.Create(CoverageConfigDefaults.Root);

    Should.Throw<ArgumentNullException>(() => PartialsAsHitsTransform.Apply(null!, config));
  }

  [Fact]
  public void Apply_NullConfig_ShouldThrow()
  {
    var report = Report(ReportFormat.Lcov, new LineCoverage(Line, 0, 1, 2, false, Missing));

    Should.Throw<ArgumentNullException>(() => PartialsAsHitsTransform.Apply(report, null!));
  }

  [Fact]
  public async Task Apply_CoberturaFixture_ShouldMatchTheRuleOfCodecov()
  {
    var totals = await ApplyToFixtureAsync(new CoberturaParser(), "cobertura", "coverlet-async.xml", "cobertura");

    // Of the 12 branch lines of the fixture, only the two that are 0/n keep their branches.
    totals.ShouldBe(new CoverageTotals(27, 11, 0, 2, 0));
  }

  [Fact]
  public async Task Apply_LcovFixture_ShouldMatchTheRuleOfCodecov()
  {
    var totals = await ApplyToFixtureAsync(new LcovParser(), "lcov", "coverlet.info", "lcov");

    totals.ShouldBe(new CoverageTotals(15, 5, 0, 6, 0));
  }

  [Fact]
  public async Task Apply_JacocoFixture_ShouldMatchTheRuleOfCodecov()
  {
    var totals = await ApplyToFixtureAsync(new JacocoParser(), "jacoco", "with-doctype.xml", "jacoco");

    totals.ShouldBe(new CoverageTotals(20, 10, 0, 2, 6));
  }

  private static async Task<CoverageTotals> ApplyToFixtureAsync(IReportParser parser, string folder, string name, string key)
  {
    var path = Path.Combine(AppContext.BaseDirectory, "fixtures", "reports", folder, name);

    await using var stream = File.OpenRead(path);

    var report = await parser.ParseAsync(stream, TestContext.Current.CancellationToken);
    var result = PartialsAsHitsTransform.Apply(report, Enabled(key));

    return CoverageReport.Create(result.Files.Select(file => FileCoverage.Create(file.RawPath, file.Lines))).Totals;
  }

  private static ParsedReport Report(ReportFormat format, LineCoverage line) =>
    new(format, [], [new ParsedFile("a.cs", [line])]);

  private static LineCoverage LineOf(ParsedReport report) => report.Files.ShouldHaveSingleItem().Lines.ShouldHaveSingleItem();

  private static CoverageConfig Enabled(params string[] keys)
  {
    var parsers = keys.Select(key => (key, ConfigNode.FromMap(("partials_as_hits", ConfigNode.FromBoolean(true)))));

    return CoverageConfig.Create(ConfigNode.FromMap(("parsers", ConfigNode.FromMap(parsers))));
  }

  #endregion
}
