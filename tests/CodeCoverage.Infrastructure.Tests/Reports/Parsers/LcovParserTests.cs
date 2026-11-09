// ------------------------------------------------------------------------------------------------
// <copyright file="LcovParserTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Text;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using CodeCoverage.Domain.Coverage;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Infrastructure.Reports.Parsers;

namespace CodeCoverage.Infrastructure.Tests.Reports.Parsers;

public sealed class LcovParserTests
{
  #region Fields

  // The three fixtures are the real output of Jest 24.9 with ts-jest (which writes absolute paths),
  // Vitest 5.1 with the v8 provider in a sub-folder, and coverlet.msbuild 10.1.0, on small samples. The Vitest report is untouched; in the others only the machine-specific prefix of the
  // paths was rewritten, to a Linux runner for Jest and to a Windows runner for coverlet. The text
  // of the other tests is hand-written or comes from test_lcov.py of codecov/umbrella. The expected
  // totals are those of Codecov's own lcov.py and merge.py, run on the fixtures.
  private const int MaxLineLength = 1024 * 1024;

  private readonly LcovParser _parser = new();

  #endregion

  #region Methods

  [Fact]
  public async Task Parse_BrdaOnDaLine_ShouldReplaceWithBranch()
  {
    var lines = await ParseLinesAsync("DA:7,731835\nBRDA:7,0,0,0\nBRDA:7,0,1,1");

    lines.ShouldBe([new LineCoverage(7, 0, 1, 2, false, ["0:0"])]);
  }

  [Fact]
  public async Task Parse_BrdaWithNoBranchTaken_ShouldBeAMissEvenWhenDaHits()
  {
    var lines = await ParseLinesAsync("DA:7,5\nBRDA:7,0,0,0\nBRDA:7,0,1,-");

    lines.ShouldBe([new LineCoverage(7, 0, 0, 2, false, ["0:0", "0:1"])]);
  }

  [Fact]
  public async Task Parse_BrdaWithoutDa_ShouldCreateTheLine()
  {
    var lines = await ParseLinesAsync("BRDA:5,1,0,1\nBRDA:5,1,1,1");

    lines.ShouldBe([new LineCoverage(5, 0, 2, 2, false, null)]);
  }

  [Theory]
  [InlineData("a.js")]
  [InlineData("src/deep/a.js")]
  public async Task Parse_JsFile_ShouldIgnoreFnAndBrda(string path)
  {
    var lines = await ParseLinesAsync("FN:1,f\nDA:1,3\nBRDA:1,0,0,0\nBRDA:2,0,0,0\nDA:3,0", path);

    lines.ShouldBe(
    [
      new LineCoverage(1, 3, 0, 0, false, null),
      new LineCoverage(3, 0, 0, 0, false, null),
    ]);
  }

  [Theory]
  [InlineData("a.jsx")]
  [InlineData("a.mjs")]
  [InlineData("a.JS")]
  [InlineData("a.ts")]
  public async Task Parse_OtherScriptExtensions_ShouldKeepBrda(string path)
  {
    var lines = await ParseLinesAsync("DA:2,3\nBRDA:2,0,0,0", path);

    lines.ShouldBe([new LineCoverage(2, 0, 0, 1, false, ["0:0"])]);
  }

  [Fact]
  public async Task Parse_TsLineOneBrda_ShouldIgnore()
  {
    var lines = await ParseLinesAsync("DA:1,1\nBRDA:1,1,0,0\nBRDA:2,1,0,1", "a.ts");

    lines.ShouldBe(
    [
      new LineCoverage(1, 1, 0, 0, false, null),
      new LineCoverage(2, 0, 1, 1, false, null),
    ]);
  }

  [Theory]
  [InlineData("a.tsx")]
  [InlineData("a.c")]
  public async Task Parse_LineOneBrdaOutsideTsFiles_ShouldKeepIt(string path)
  {
    var lines = await ParseLinesAsync("BRDA:1,1,0,0", path);

    lines.ShouldBe([new LineCoverage(1, 0, 0, 1, false, ["1:0"])]);
  }

  [Fact]
  public async Task Parse_RepeatedSf_ShouldMerge()
  {
    var report = await ParseAsync(
      """
      TN:
      SF:a.c
      DA:1,1
      DA:2,0
      DA:3,2
      BRDA:4,0,0,1
      BRDA:4,0,1,0
      end_of_record
      TN:
      SF:b.c
      DA:1,1
      end_of_record
      TN:
      SF:a.c
      DA:2,5
      DA:3,1
      BRDA:4,0,0,0
      BRDA:4,0,1,1
      DA:9,0
      end_of_record
      """);

    report.Files.Select(file => file.RawPath).ShouldBe(["a.c", "b.c"]);
    report.Files[0].Lines.ShouldBe(
    [
      new LineCoverage(1, 1, 0, 0, false, null),
      new LineCoverage(2, 5, 0, 0, false, null),
      new LineCoverage(3, 2, 0, 0, false, null),
      new LineCoverage(4, 0, 2, 2, false, []),
      new LineCoverage(9, 0, 0, 0, false, null),
    ]);
  }

  [Fact]
  public async Task Parse_RepeatedSfWithExecutedPlainLine_ShouldCoverTheBranches()
  {
    var report = await ParseAsync("SF:a.c\nBRDA:4,0,0,0\nBRDA:4,0,1,1\nend_of_record\nSF:a.c\nDA:4,3\nend_of_record\n");

    report.Files.ShouldHaveSingleItem().Lines.ShouldBe([new LineCoverage(4, 3, 2, 2, false, [])]);
  }

  [Fact]
  public async Task Parse_DuplicateDaLines_ShouldKeepTheLargestHits()
  {
    var lines = await ParseLinesAsync("DA:1,1\nDA:1,4\nDA:1,2");

    lines.ShouldHaveSingleItem().Hits.ShouldBe(4);
  }

  [Fact]
  public async Task Parse_SameBranchTwice_ShouldKeepTheLastValue()
  {
    var lines = await ParseLinesAsync("BRDA:2,0,0,1\nBRDA:2,0,1,1\nBRDA:2,0,0,0");

    lines.ShouldBe([new LineCoverage(2, 0, 1, 2, false, ["0:0"])]);
  }

  [Theory]
  [InlineData("0", false)]
  [InlineData("-", false)]
  [InlineData("1", true)]
  [InlineData("00", true)]
  [InlineData("abc", true)]
  [InlineData("", true)]
  public async Task Parse_BranchTaken_ShouldFollowCodecov(string taken, bool isCovered)
  {
    var lines = await ParseLinesAsync($"BRDA:2,0,0,{taken}");

    lines.ShouldHaveSingleItem().Covered.ShouldBe(isCovered ? 1 : 0);
  }

  [Theory]
  [InlineData("BRDA:0,1,0,1")]
  [InlineData("BRDA:,1,0,1")]
  [InlineData("BRDA:x,1,0,1")]
  [InlineData("BRDA:-1,1,0,1")]
  [InlineData("BRDA:1,2,3")]
  public async Task Parse_BrdaNotUsable_ShouldSkipIt(string entry)
  {
    var lines = await ParseLinesAsync($"{entry}\nDA:9,1");

    lines.ShouldBe([new LineCoverage(9, 1, 0, 0, false, null)]);
  }

  [Fact]
  public async Task Parse_FunctionOnBranchLine_ShouldMarkTheLineAsMethod()
  {
    var lines = await ParseLinesAsync("FN:2,not_hit\nBRDA:2,1,0,1\nBRDA:2,1,1,-\nBRDA:2,1,3,0", "file.cpp");

    lines.ShouldBe([new LineCoverage(2, 0, 1, 3, true, ["1:1", "1:3"])]);
  }

  [Fact]
  public async Task Parse_FunctionWithoutBranch_ShouldNotCreateALine()
  {
    var lines = await ParseLinesAsync("FN:2,f\nFNDA:1,f\nDA:3,1");

    lines.ShouldBe([new LineCoverage(3, 1, 0, 0, false, null)]);
  }

  [Theory]
  [InlineData("_Zfoo")]
  [InlineData("_Gbar")]
  public async Task Parse_CppMangledFunction_ShouldDropTheBranchesOfItsLine(string name)
  {
    var lines = await ParseLinesAsync($"FN:3,{name}\nDA:3,1\nBRDA:3,0,0,0\nBRDA:4,0,0,1", "file.cpp");

    lines.ShouldBe(
    [
      new LineCoverage(3, 1, 0, 0, false, null),
      new LineCoverage(4, 0, 1, 1, false, null),
    ]);
  }

  [Theory]
  [InlineData("file.cc")]
  [InlineData("file.CPP")]
  public async Task Parse_MangledFunctionOutsideCppFiles_ShouldMarkTheLineAsMethod(string path)
  {
    var lines = await ParseLinesAsync("FN:3,_Zfoo\nBRDA:3,0,0,1", path);

    lines.ShouldBe([new LineCoverage(3, 0, 1, 1, true, null)]);
  }

  [Theory]
  [InlineData("3", 3)]
  [InlineData("1e+0", 1)]
  [InlineData("1E3", 1000)]
  [InlineData("2.9", 2)]
  [InlineData("0.5", 0)]
  [InlineData("-1", 0)]
  [InlineData("-5", 0)]
  [InlineData("-0.5", 0)]
  [InlineData("99999999999999999999", long.MaxValue)]
  [InlineData("1.84467e+300", long.MaxValue)]
  public async Task Parse_Hits_ShouldReadThemLikeCodecovWithinBounds(string hits, long expected)
  {
    var lines = await ParseLinesAsync($"DA:1,{hits}");

    lines.ShouldHaveSingleItem().Hits.ShouldBe(expected);
  }

  [Fact]
  public async Task Parse_DaWithChecksum_ShouldIgnoreTheChecksum()
  {
    var lines = await ParseLinesAsync("DA:1,1,46ba21aa66ea047aced7130c2760d7d4");

    lines.ShouldBe([new LineCoverage(1, 1, 0, 0, false, null)]);
  }

  // The corrupt report of test_lcov.py, and what real tools or errors can write in the two fields.
  [Theory]
  [InlineData("DA:DA:130,0")]
  [InlineData("DA:0,")]
  [InlineData("DA:,0")]
  [InlineData("DA:")]
  [InlineData("DA:4")]
  [InlineData("DA:not_int,123")]
  [InlineData("DA:123,not_decimal")]
  [InlineData("DA:0,1")]
  [InlineData("DA:012,1")]
  [InlineData("DA:undefined,1")]
  [InlineData("DA:null,skipped")]
  [InlineData("DA:=,=")]
  [InlineData("DA:2,undefined")]
  [InlineData("DA:2,-")]
  [InlineData("DA:2,NaN")]
  [InlineData("DA:2,Infinity")]
  [InlineData("DA:-1,1")]
  [InlineData("DA:99999999999,1")]
  [InlineData("FN:just_a_name_no_line")]
  [InlineData("removed")]
  [InlineData("da:2,1")]
  [InlineData(" DA:2,1")]
  [InlineData("")]
  public async Task Parse_MalformedEntry_ShouldSkipIt(string entry)
  {
    var lines = await ParseLinesAsync($"DA:1,1\n{entry}\n\nDA:9,1");

    lines.Select(line => line.Line).ShouldBe([1, 9]);
  }

  [Fact]
  public async Task Parse_FileWithoutLines_ShouldNotEmitAFile()
  {
    var report = await ParseAsync("SF:a.c\nFN:1,f\nend_of_record\n");

    report.Files.ShouldBeEmpty();
  }

  [Theory]
  [InlineData("")]
  [InlineData("TN:\n")]
  [InlineData("end_of_record\n")]
  public async Task Parse_ReportWithoutFile_ShouldEmitNothing(string text)
  {
    var report = await ParseAsync(text);

    report.Format.ShouldBe(ReportFormat.Lcov);
    report.SourceRoots.ShouldBeEmpty();
    report.Files.ShouldBeEmpty();
  }

  [Fact]
  public async Task Parse_LastRecordWithoutEndOfRecord_ShouldBeKept()
  {
    var report = await ParseAsync("SF:a.c\nDA:1,1\nend_of_record\nSF:b.c\nDA:2,1");

    report.Files.Select(file => file.RawPath).ShouldBe(["a.c", "b.c"]);
  }

  // Codecov replaces the file of the record on a second SF, and keeps the branches read so far.
  [Fact]
  public async Task Parse_SecondSfInARecord_ShouldDropTheLinesOfTheFirst()
  {
    var report = await ParseAsync("SF:a.c\nDA:1,1\nBRDA:2,0,0,1\nSF:b.c\nDA:3,1\nend_of_record\n");

    report.Files.ShouldHaveSingleItem().RawPath.ShouldBe("b.c");
    report.Files[0].Lines.ShouldBe(
    [
      new LineCoverage(2, 0, 1, 1, false, null),
      new LineCoverage(3, 1, 0, 0, false, null),
    ]);
  }

  [Theory]
  [InlineData("DA:1,1\nSF:a.c\nDA:2,1")]
  [InlineData("foo:bar\nSF:a.c\nDA:2,1")]
  [InlineData("SF:\nDA:1,1\nSF:a.c\nDA:2,1")]
  public async Task Parse_EntryBeforeAnyFile_ShouldDropTheRecord(string record)
  {
    var report = await ParseAsync($"{record}\nend_of_record\nSF:b.c\nDA:3,1\nend_of_record\n");

    report.Files.ShouldHaveSingleItem().RawPath.ShouldBe("b.c");
  }

  [Theory]
  [InlineData("TN:x\nFNF:1\nLF:1\nSF:a.c")]
  [InlineData("noise\n\nSF:a.c")]
  [InlineData("SF:\nSF:a.c")]
  public async Task Parse_IgnoredEntriesBeforeTheFile_ShouldKeepTheRecord(string head)
  {
    var report = await ParseAsync($"{head}\nDA:2,1\nend_of_record\n");

    report.Files.ShouldHaveSingleItem().RawPath.ShouldBe("a.c");
  }

  [Theory]
  [InlineData("end_of_recordXYZ\n")]
  [InlineData("end_of_record   \n")]
  [InlineData("end_of_record\r\n")]
  public async Task Parse_EndOfRecordWithTrailingText_ShouldStillCloseTheRecord(string end)
  {
    var report = await ParseAsync($"SF:a.c\nDA:1,1\n{end}SF:b.c\nDA:1,1\nend_of_record\n");

    report.Files.Select(file => file.RawPath).ShouldBe(["a.c", "b.c"]);
  }

  [Fact]
  public async Task Parse_WindowsLineEndings_ShouldReadEachEntry()
  {
    var report = await ParseAsync("SF:C:\\src\\a.c\r\nDA:1,1\r\nBRDA:2,0,0,1\r\nend_of_record\r\n");

    report.Files.ShouldHaveSingleItem().RawPath.ShouldBe(@"C:\src\a.c");
    report.Files[0].Lines.Select(line => line.Line).ShouldBe([1, 2]);
  }

  [Fact]
  public async Task Parse_TrailingSpacesOnThePath_ShouldBeRemoved()
  {
    var report = await ParseAsync("SF: a.js \nDA:1,1\nBRDA:1,0,0,0\nend_of_record\n");

    report.Files.ShouldHaveSingleItem().RawPath.ShouldBe("a.js");
    report.Files[0].Lines.ShouldBe([new LineCoverage(1, 1, 0, 0, false, null)]);
  }

  [Fact]
  public async Task Parse_ByteOrderMark_ShouldReadTheFirstEntry()
  {
    using var report = new MemoryStream([.. Encoding.UTF8.Preamble, .. "SF:a.c\nDA:1,1\nend_of_record\n"u8.ToArray()]);

    var parsed = await _parser.ParseAsync(report, TestContext.Current.CancellationToken);

    parsed.Files.ShouldHaveSingleItem().RawPath.ShouldBe("a.c");
  }

  [Fact]
  public async Task Parse_InvalidUtf8_ShouldReplaceTheBytes()
  {
    using var report = new MemoryStream([.. "SF:a"u8.ToArray(), 0xFF, .. ".c\nDA:1,1\nend_of_record\n"u8.ToArray()]);

    var parsed = await _parser.ParseAsync(report, TestContext.Current.CancellationToken);

    parsed.Files.ShouldHaveSingleItem().RawPath.ShouldBe("a\uFFFD.c");
  }

  [Fact]
  public async Task Parse_LineOverTheLimit_ShouldDropItAndReadTheNext()
  {
    var lines = await ParseLinesAsync($"DA:1,1\nFN:{new string('x', MaxLineLength * 3)},name\nDA:2,1");

    lines.Select(line => line.Line).ShouldBe([1, 2]);
  }

  [Fact]
  public async Task Parse_LastLineOverTheLimit_ShouldDropIt()
  {
    var report = await ParseAsync($"SF:a.c\nDA:1,1\nend_of_record\n{new string('x', MaxLineLength * 2)}");

    report.Files.ShouldHaveSingleItem().Lines.ShouldHaveSingleItem();
  }

  [Theory]
  [InlineData(MaxLineLength - 1, 1)]
  [InlineData(MaxLineLength, 0)]
  public async Task Parse_LineAroundTheLimit_ShouldKeepOnlyShorterOnes(int length, int expectedFiles)
  {
    var report = await ParseAsync($"SF:{new string('a', length - 3)}\nDA:1,1\nend_of_record\n");

    report.Files.Count.ShouldBe(expectedFiles);
  }

  [Fact]
  public async Task Parse_ReportLargerThanTheWindow_ShouldReadEveryLine()
  {
    const int LineCount = 250_000;

    var text = new StringBuilder("SF:a.c\n");

    for (var line = 1; line <= LineCount; line++)
    {
      text.Append("DA:").Append(line).Append(",1\n");
    }

    var report = await ParseAsync(text.Append("end_of_record\n").ToString());

    report.Files.ShouldHaveSingleItem().Lines.Count.ShouldBe(LineCount);
  }

  [Theory]
  [InlineData("jest-absolute.info", 2, 6, 4, 2, 3)]
  [InlineData("vitest-relative.info", 2, 2, 3, 2, 3)]
  [InlineData("coverlet.info", 2, 12, 5, 3, 6)]
  public async Task Parse_EachFixture_ShouldMatchCodecovTotals(string fixture, int files, int hits, int misses, int partials, int branches)
  {
    var report = await ParseFixtureAsync(fixture);

    var totals = CoverageReport.Create(report.Files.Select(file => FileCoverage.Create(file.RawPath, file.Lines))).Totals;

    report.Format.ShouldBe(ReportFormat.Lcov);
    report.Files.Count.ShouldBe(files);
    totals.ShouldBe(new CoverageTotals(hits, misses, partials, branches, 0));
  }

  [Fact]
  public async Task Parse_JestFixture_ShouldKeepAbsolutePathsAndIgnoreJsBranches()
  {
    var report = await ParseFixtureAsync("jest-absolute.info");

    report.Files.Select(file => file.RawPath)
          .ShouldBe(["/home/runner/work/sample/sample/src/math.js", "/home/runner/work/sample/sample/src/service.ts"]);
    report.Files[0].Lines.ShouldAllBe(line => !line.IsBranch);
    report.Files[1].Lines.Single(line => line.Line == 2).ShouldBe(new LineCoverage(2, 0, 1, 2, false, ["0:0"]));
  }

  [Fact]
  public async Task Parse_VitestFixture_ShouldKeepPathsRelativeToTheSubFolder()
  {
    var report = await ParseFixtureAsync("vitest-relative.info");

    report.Files.Select(file => file.RawPath).ShouldBe([@"src\format.ts", @"src\legacy.js"]);
  }

  [Fact]
  public async Task Parse_CoverletFixture_ShouldReadAllBlocksOfALine()
  {
    var report = await ParseFixtureAsync("coverlet.info");

    var calculator = report.Files.Single(file => file.RawPath.EndsWith("Calculator.cs", StringComparison.Ordinal));

    calculator.Lines.Single(line => line.Line == 12).ShouldBe(new LineCoverage(12, 0, 2, 4, false, ["16:1", "20:0"]));
  }

  [Fact]
  public async Task Parse_CancelledToken_ShouldThrowOperationCanceledException()
  {
    using var report = new MemoryStream("SF:a.c\nDA:1,1\nend_of_record\n"u8.ToArray());

    await Should.ThrowAsync<OperationCanceledException>(() => _parser.ParseAsync(report, new CancellationToken(canceled: true)));
  }

  [Fact]
  public async Task Parse_NullReport_ShouldThrowArgumentNullException()
  {
    await Should.ThrowAsync<ArgumentNullException>(() => _parser.ParseAsync(null!, TestContext.Current.CancellationToken));
  }

  [Fact]
  public async Task Parse_ShouldLeaveTheStreamOpen()
  {
    using var report = new MemoryStream("SF:a.c\nDA:1,1\nend_of_record\n"u8.ToArray());

    await _parser.ParseAsync(report, TestContext.Current.CancellationToken);

    report.CanRead.ShouldBeTrue();
  }

  [Fact]
  public void Format_ShouldBeLcov()
  {
    _parser.Format.ShouldBe(ReportFormat.Lcov);
  }

  [Fact]
  public async Task AddInfrastructure_ShouldRegisterTheLcovParser()
  {
    await using var services = new ServiceCollection()
                               .AddSingleton<IConfiguration>(new ConfigurationBuilder().Build())
                               .AddSingleton(A.Fake<ISecretProtector>())
                               .AddLogging()
                               .AddInfrastructure()
                               .BuildServiceProvider();

    services.GetRequiredService<IReportParserCatalog>().Find(ReportFormat.Lcov).ShouldBeOfType<LcovParser>();
  }

  private async Task<IReadOnlyList<LineCoverage>> ParseLinesAsync(string entries, string path = "a.c")
  {
    var report = await ParseAsync($"TN:\nSF:{path}\n{entries}\nend_of_record\n");

    return report.Files.Count == 0 ? [] : report.Files.ShouldHaveSingleItem().Lines;
  }

  private async Task<ParsedReport> ParseAsync(string text)
  {
    using var report = new MemoryStream(Encoding.UTF8.GetBytes(text));

    return await _parser.ParseAsync(report, TestContext.Current.CancellationToken);
  }

  private async Task<ParsedReport> ParseFixtureAsync(string name)
  {
    var path = Path.Combine(AppContext.BaseDirectory, "fixtures", "reports", "lcov", name);

    await using var report = File.OpenRead(path);

    return await _parser.ParseAsync(report, TestContext.Current.CancellationToken);
  }

  #endregion
}
