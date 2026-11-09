// ------------------------------------------------------------------------------------------------
// <copyright file="ParserOracleTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Text.Json.Nodes;

using CodeCoverage.Domain.Coverage;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Infrastructure.Reports.Parsers;

namespace CodeCoverage.Infrastructure.Tests.Reports.Oracle;

public sealed class ParserOracleTests
{
  #region Fields

  // The parsers follow Codecov and ReportGenerator does not always: a difference is not a defect, but
  // it has to be known. Each one is listed in expected-totals.json with the rule that explains it, so
  // an update of a parser or of the oracle that moves a line fails here.
  private const string Absent = "absent";

  private static readonly string ReportsDirectory = Path.Combine(AppContext.BaseDirectory, "fixtures", "reports");

  private static readonly Dictionary<string, IReportParser> Parsers = new(StringComparer.Ordinal)
  {
    ["cobertura"] = new CoberturaParser(),
    ["opencover"] = new OpenCoverParser(),
    ["lcov"] = new LcovParser(),
    ["jacoco"] = new JacocoParser(),
    ["clover"] = new CloverParser(),
  };

  private static readonly JsonNode Expected =
    JsonNode.Parse(File.ReadAllText(Path.Combine(ReportsDirectory, "expected-totals.json")))!;

  #endregion

  #region Methods

  public static TheoryData<string> Fixtures() => [.. FixtureNames()];

  [Theory]
  [MemberData(nameof(Fixtures))]
  public async Task Parse_EachFixture_ShouldMatchOracleLineCounts(string fixture)
  {
    var path = Path.Combine(ReportsDirectory, fixture);
    var parsed = await ParseAsync(fixture, path);
    var oracle = ReportGeneratorOracle.Read(path);
    var expected = Expected["fixtures"]![fixture]!;

    TotalsOf(parsed).ShouldBe(ReadParserTotals(expected["parser"]!));
    TotalsOf(oracle).ShouldBe(ReadOracleTotals(expected["oracle"]!));
    Compare(parsed, oracle).ShouldBe(ReadDifferences(expected));
  }

  [Fact]
  public void ExpectedTotals_Fixtures_ShouldBeThoseOnDisk()
  {
    var listed = Expected["fixtures"]!.AsObject().Select(entry => entry.Key).Order(StringComparer.Ordinal);

    listed.ShouldBe(FixtureNames().Order(StringComparer.Ordinal));
  }

  [Fact]
  public void ExpectedTotals_EachDifference_ShouldNameAnExplainedRule()
  {
    var rules = Expected["rules"]!.AsObject();
    var used = Expected["fixtures"]!
      .AsObject()
      .SelectMany(entry => entry.Value!["differences"]!.AsArray())
      .Select(difference => difference!["rule"]!.GetValue<string>())
      .Distinct(StringComparer.Ordinal)
      .ToList();

    used.ShouldBe(rules.Select(rule => rule.Key), ignoreOrder: true);

    foreach (var (_, rule) in rules)
    {
      rule!["codecov"]!.GetValue<string>().ShouldNotBeNullOrWhiteSpace();
      rule["reportGenerator"]!.GetValue<string>().ShouldNotBeNullOrWhiteSpace();
      rule["parserFollows"]!.GetValue<string>().ShouldNotBeNullOrWhiteSpace();
    }
  }

  private static IEnumerable<string> FixtureNames() =>
    Parsers.Keys.SelectMany(
      folder => Directory
        .EnumerateFiles(Path.Combine(ReportsDirectory, folder))
        .Select(file => $"{folder}/{Path.GetFileName(file)}"));

  private static async Task<ParsedReport> ParseAsync(string fixture, string path)
  {
    await using var report = File.OpenRead(path);

    return await Parsers[fixture.Split('/')[0]].ParseAsync(report, TestContext.Current.CancellationToken);
  }

  private static (int Lines, int Hits, int Misses, int Partials, int Branches, int Methods) TotalsOf(ParsedReport report)
  {
    var totals = CoverageReport.Create(report.Files.Select(file => FileCoverage.Create(file.RawPath, file.Lines))).Totals;

    return (totals.Lines, totals.Hits, totals.Misses, totals.Partials, totals.Branches, totals.Methods);
  }

  private static (int Lines, int Hits, int Misses, int Partials) TotalsOf(
    IReadOnlyDictionary<string, IReadOnlyDictionary<int, OracleLine>> oracle)
  {
    var lines = oracle.Values.SelectMany(file => file.Values).ToList();

    return (lines.Count, Count(lines, LineState.Hit), Count(lines, LineState.Miss), Count(lines, LineState.Partial));
  }

  private static int Count(List<OracleLine> lines, LineState state) => lines.Count(line => line.State == state);

  private static (int Lines, int Hits, int Misses, int Partials, int Branches, int Methods) ReadParserTotals(JsonNode node) =>
    (Int(node, "lines"), Int(node, "hits"), Int(node, "misses"), Int(node, "partials"), Int(node, "branches"), Int(node, "methods"));

  private static (int Lines, int Hits, int Misses, int Partials) ReadOracleTotals(JsonNode node) =>
    (Int(node, "lines"), Int(node, "hits"), Int(node, "misses"), Int(node, "partials"));

  private static int Int(JsonNode node, string name) => node[name]!.GetValue<int>();

  private static List<string> ReadDifferences(JsonNode fixture) =>
    fixture["differences"]!
      .AsArray()
      .Select(
        difference => Difference(
          difference!["file"]!.GetValue<string>(),
          Int(difference, "line"),
          difference["parser"]!.GetValue<string>(),
          difference["oracle"]!.GetValue<string>()))
      .ToList();

  private static List<string> Compare(
    ParsedReport parsed,
    IReadOnlyDictionary<string, IReadOnlyDictionary<int, OracleLine>> oracle)
  {
    var orphans = new HashSet<string>(oracle.Keys, StringComparer.Ordinal);
    var differences = new List<string>();

    foreach (var file in parsed.Files.OrderBy(file => file.RawPath, StringComparer.Ordinal))
    {
      var path = oracle.Keys.Single(candidate => IsSameFile(file.RawPath, candidate));

      orphans.Remove(path);
      differences.AddRange(CompareFile(file, oracle[path]));
    }

    orphans.ShouldBeEmpty();

    return differences;
  }

  private static IEnumerable<string> CompareFile(ParsedFile file, IReadOnlyDictionary<int, OracleLine> oracle)
  {
    var lines = file.Lines.ToDictionary(line => line.Line);

    foreach (var number in lines.Keys.Union(oracle.Keys).Order())
    {
      var ours = lines.TryGetValue(number, out var line)
        ? Describe(CoverageMath.Classify(line), line.Covered, line.Total)
        : Absent;
      var theirs = oracle.TryGetValue(number, out var oracleLine)
        ? Describe(oracleLine.State, oracleLine.CoveredBranches, oracleLine.TotalBranches)
        : Absent;

      if (!string.Equals(ours, theirs, StringComparison.Ordinal))
      {
        yield return Difference(file.RawPath, number, ours, theirs);
      }
    }
  }

  private static string Describe(LineState state, int covered, int total) => $"{state} {covered}/{total}";

  private static string Difference(string file, int line, string parser, string oracle) =>
    $"{file}:{line} parser={parser} oracle={oracle}";

  // ReportGenerator joins a relative path with the sources, drops the package of a JaCoCo path and writes
  // the separators of the machine: only the end of a path is comparable.
  private static bool IsSameFile(string parserPath, string oraclePath)
  {
    var ours = Normalize(parserPath);
    var theirs = Normalize(oraclePath);

    return string.Equals(ours, theirs, StringComparison.Ordinal)
      || ours.EndsWith("/" + theirs, StringComparison.Ordinal)
      || theirs.EndsWith("/" + ours, StringComparison.Ordinal);
  }

  private static string Normalize(string path) => path.Replace('\\', '/').TrimStart('/');

  #endregion
}
