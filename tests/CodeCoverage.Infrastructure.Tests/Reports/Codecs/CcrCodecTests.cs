// ------------------------------------------------------------------------------------------------
// <copyright file="CcrCodecTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.IO.Compression;

using CodeCoverage.Domain.Coverage;
using CodeCoverage.Infrastructure.Reports.Codecs;

namespace CodeCoverage.Infrastructure.Tests.Reports.Codecs;

public sealed class CcrCodecTests
{
  #region Fields

  private const int HeaderLength = 6;
  private const int VersionOffset = 4;
  private const int FlagsOffset = 5;

  // One string "a", one file with one line: the valid base of the crafted bodies.
  private const string ValidBody = "010161" + "01" + "0001" + "010001";

  // The body of ReferenceReport, written by hand from section 4.3 of the architecture. It freezes
  // the layout of version 1, together with tests/fixtures/ccr/v1-sample.bin.
  private const string ReferenceBody =
    "04" + "08" + "7372632F612E6373" + "09" + "7372632FC3A92E6373" + "02" + "6230" + "02" + "6231"
    + "02"
    + "0004" + "010003" + "010000" + "08050101020102" + "A2020205"
    + "0103" + "0100FFFFFFFF0F" + "0107AC02020200" + "0105000002020203";

  private readonly CcrCodec _codec = new();

  #endregion

  #region Methods

  [Fact]
  public void RoundTrip_RandomReports_ShouldBeLossless()
  {
    for (var seed = 0; seed < 300; seed++)
    {
      var report = RandomReport(seed);

      AssertSameReport(_codec.Decode(_codec.Encode(report)), report);
    }
  }

  [Fact]
  public void RoundTrip_EmptyReport_ShouldGiveAnEmptyReport()
  {
    _codec.Decode(_codec.Encode(CoverageReport.Create([]))).Files.ShouldBeEmpty();
  }

  [Fact]
  public void RoundTrip_NullAndEmptyMissingBranches_ShouldStayDifferent()
  {
    var report = Report(
      "a.cs",
      new LineCoverage(1, 1, 1, 2, false, null),
      new LineCoverage(2, 1, 2, 2, false, []));

    var lines = _codec.Decode(_codec.Encode(report)).Files[0].Lines;

    lines[0].MissingBranches.ShouldBeNull();
    lines[1].MissingBranches.ShouldBeEmpty();
  }

  [Theory]
  [InlineData(uint.MaxValue + 1L, uint.MaxValue)]
  [InlineData(long.MaxValue, uint.MaxValue)]
  [InlineData(-5L, 0L)]
  public void Encode_HitsOutOfRange_ShouldBeClampedWithoutChangingTheState(long hits, long expected)
  {
    var line = new LineCoverage(1, hits, 0, 0, false, null);

    var decoded = _codec.Decode(_codec.Encode(Report("a.cs", line))).Files[0].Lines[0];

    decoded.Hits.ShouldBe(expected);
    CoverageMath.Classify(decoded).ShouldBe(CoverageMath.Classify(line));
  }

  [Theory]
  [InlineData(-1, 2)]
  [InlineData(1, -2)]
  public void Encode_NegativeBranchCounter_ShouldThrow(int covered, int total)
  {
    var report = Report("a.cs", new LineCoverage(1, 1, covered, total, false, null));

    Should.Throw<ArgumentOutOfRangeException>(() => _codec.Encode(report));
  }

  [Fact]
  public void Encode_NullReport_ShouldThrow()
  {
    Should.Throw<ArgumentNullException>(() => _codec.Encode(null!));
  }

  [Fact]
  public void Encode_5000Files_ShouldStayUnder500Kb()
  {
    var random = new Random(5000);
    var report = CoverageReport.Create(Enumerable.Range(0, 5000).Select(i => MonorepoFile(i, random)));

    var blob = _codec.Encode(report);

    report.Totals.Lines.ShouldBeGreaterThan(500_000);
    blob.Length.ShouldBeLessThan(500 * 1024);
  }

  [Fact]
  public void Encode_ReferenceReport_ShouldWriteTheFrozenHeaderAndBody()
  {
    var blob = _codec.Encode(ReferenceReport());

    blob[..HeaderLength].ShouldBe([(byte)'C', (byte)'C', (byte)'R', (byte)'1', 1, 0]);
    Convert.ToHexString(DecompressBody(blob)).ShouldBe(ReferenceBody);
  }

  [Fact]
  public void ReferenceFile_ShouldHoldTheFrozenHeaderAndBody()
  {
    var blob = ReadReferenceFile();

    blob[..HeaderLength].ShouldBe([(byte)'C', (byte)'C', (byte)'R', (byte)'1', 1, 0]);
    Convert.ToHexString(DecompressBody(blob)).ShouldBe(ReferenceBody);
  }

  [Fact]
  public void Decode_ReferenceFile_ShouldGiveTheReferenceReport()
  {
    AssertSameReport(_codec.Decode(ReadReferenceFile()), ReferenceReport());
  }

  [Theory]
  [InlineData(0)]
  [InlineData(2)]
  [InlineData(255)]
  public void Decode_UnknownVersion_ShouldThrow(byte version)
  {
    var blob = _codec.Encode(ReferenceReport());
    blob[VersionOffset] = version;

    Should.Throw<InvalidDataException>(() => _codec.Decode(blob)).Message.ShouldContain("not supported");
  }

  [Fact]
  public void Decode_UnknownFlags_ShouldThrow()
  {
    var blob = _codec.Encode(ReferenceReport());
    blob[FlagsOffset] = 1;

    Should.Throw<InvalidDataException>(() => _codec.Decode(blob)).Message.ShouldContain("flags");
  }

  [Fact]
  public void Decode_WrongMagic_ShouldThrow()
  {
    var blob = _codec.Encode(ReferenceReport());
    blob[0] = (byte)'X';

    Should.Throw<InvalidDataException>(() => _codec.Decode(blob)).Message.ShouldContain("magic");
  }

  [Fact]
  public void Decode_EveryTruncation_ShouldThrow()
  {
    var blob = _codec.Encode(ReferenceReport());

    for (var length = 0; length < blob.Length; length++)
    {
      var truncated = blob[..length];

      Should.Throw<InvalidDataException>(() => _codec.Decode(truncated), $"length {length}");
    }
  }

  [Fact]
  public void Decode_BytesAfterTheCompressedBody_ShouldThrow()
  {
    byte[] blob = [.. _codec.Encode(ReferenceReport()), 0];

    Should.Throw<InvalidDataException>(() => _codec.Decode(blob)).Message.ShouldContain("follow");
  }

  [Fact]
  public void Decode_GarbageBody_ShouldThrow()
  {
    byte[] blob = [(byte)'C', (byte)'C', (byte)'R', (byte)'1', 1, 0, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF];

    Should.Throw<InvalidDataException>(() => _codec.Decode(blob));
  }

  [Fact]
  public void Decode_AnySingleBitFlip_ShouldReportOrDecodeButNeverEscape()
  {
    var blob = _codec.Encode(ReferenceReport());

    for (var bit = 0; bit < blob.Length * 8; bit++)
    {
      var corrupted = (byte[])blob.Clone();
      corrupted[bit / 8] ^= (byte)(1 << (bit % 8));

      try
      {
        _codec.Decode(corrupted);
      }
      catch (InvalidDataException)
      {
        // Brotli has no checksum: a flip either breaks a structure or gives another valid report.
      }
    }
  }

  [Fact]
  public void Decode_ValidCraftedBody_ShouldGiveTheReport()
  {
    var report = _codec.Decode(Crafted(ValidBody));

    report.Files.Single().Path.ShouldBe("a");
    report.Files[0].Lines.Single().ShouldBe(new LineCoverage(1, 1, 0, 0, false, null));
  }

  [Theory]
  [InlineData("FFFFFFFF0F", "out of range")] // string count beyond an integer
  [InlineData("FFFFFFFF07", "count exceeds")] // string count far beyond the data
  [InlineData("01FF0161", "count exceeds")] // string length beyond the data
  [InlineData("0101FF", "UTF-8")]
  [InlineData("010161" + "FFFFFFFF07", "count exceeds")] // file count far beyond the data
  [InlineData("010161" + "01" + "05" + "01" + "010001", "index is out of range")]
  [InlineData("010120" + "01" + "00" + "01" + "010001", "blank")]
  [InlineData("010161" + "01" + "00" + "00" + "000000", "no line")]
  [InlineData("010161" + "01" + "00" + "FFFFFFFF07", "count exceeds")] // line count far beyond the data
  [InlineData("010161" + "01" + "00" + "01" + "000001", "do not increase")]
  [InlineData("010161" + "01" + "00" + "01" + "FFFFFFFF0F" + "0001", "line number is out of range")]
  [InlineData("010161" + "01" + "00" + "02" + "FFFFFFFF07" + "0001" + "010001", "line number is out of range")]
  [InlineData("010161" + "01" + "00" + "01" + "010801", "unknown shape")]
  [InlineData("010161" + "01" + "00" + "01" + "0100" + "8080808010", "exceeds 32 bits")]
  [InlineData("010161" + "01" + "00" + "01" + "0100" + "FFFFFFFFFF", "exceeds 32 bits")]
  [InlineData("010161" + "01" + "00" + "01" + "0100" + "8080", "ends too early")]
  [InlineData("010161" + "01" + "00" + "01" + "010101" + "FFFFFFFF0F" + "01", "out of range")] // covered
  [InlineData("010161" + "01" + "00" + "01" + "010101", "ends too early")]
  [InlineData("010161" + "01" + "00" + "01" + "010401" + "FFFFFFFF07", "count exceeds")] // missing count
  [InlineData("010161" + "01" + "00" + "01" + "010401" + "0109", "index is out of range")]
  [InlineData(ValidBody + "00", "unexpected bytes")]
  [InlineData("020161" + "0161" + "02" + "0001010001" + "0101010001", "sorted and distinct")]
  [InlineData("020162" + "0161" + "02" + "0001010001" + "0101010001", "sorted and distinct")]
  public void Decode_CorruptedBody_ShouldThrowAClearError(string body, string reason)
  {
    Should.Throw<InvalidDataException>(() => _codec.Decode(Crafted(body))).Message.ShouldContain(reason);
  }

  [Fact]
  public void Open_BodyBeyondTheLimit_ShouldThrow()
  {
    var bomb = CcrCodec.Seal("CCR1"u8, new byte[1024 * 1024]);

    var exception = Should.Throw<InvalidDataException>(() => CcrCodec.Open("CCR1"u8, bomb, 1024));

    exception.Message.ShouldContain("exceeds 1024 bytes");
  }

  [Fact]
  public void Open_BodyExactlyAtTheLimit_ShouldBeAccepted()
  {
    var blob = CcrCodec.Seal("CCR1"u8, new byte[1024]);

    CcrCodec.Open("CCR1"u8, blob, 1024).Length.ShouldBe(1024);
  }

  private static byte[] Crafted(string hexBody) => CcrCodec.Seal("CCR1"u8, Convert.FromHexString(hexBody));

  private static byte[] ReadReferenceFile() =>
    File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "fixtures", "ccr", "v1-sample.bin"));

  private static byte[] DecompressBody(byte[] blob)
  {
    using var input = new MemoryStream(blob, HeaderLength, blob.Length - HeaderLength);
    using var brotli = new BrotliStream(input, CompressionMode.Decompress);
    using var output = new MemoryStream();

    brotli.CopyTo(output);

    return output.ToArray();
  }

  private static CoverageReport Report(string path, params LineCoverage[] lines) =>
    CoverageReport.Create([FileCoverage.Create(path, lines)]);

  private static CoverageReport ReferenceReport() =>
    CoverageReport.Create(
    [
      FileCoverage.Create(
        "src/a.cs",
        [
          new LineCoverage(1, 3, 0, 0, false, null),
          new LineCoverage(2, 0, 0, 0, false, null),
          new LineCoverage(10, 1, 1, 2, false, ["b0"]),
          new LineCoverage(300, 5, 0, 0, true, null),
        ]),
      FileCoverage.Create(
        "src/é.cs",
        [
          new LineCoverage(1, uint.MaxValue, 0, 0, false, null),
          new LineCoverage(2, 300, 2, 2, true, []),
          new LineCoverage(3, 0, 0, 2, false, ["b0", "b1"]),
        ]),
    ]);

  private static CoverageReport RandomReport(int seed)
  {
    var random = new Random(seed);
    var files = Enumerable.Range(0, random.Next(0, 12))
      .Select(i => FileCoverage.Create($"src/é{random.Next(1000)}/{i}.cs", RandomLines(random)));

    return CoverageReport.Create([.. files]);
  }

  private static LineCoverage[] RandomLines(Random random)
  {
    var count = random.Next(1, 40);
    var lines = new LineCoverage[count];

    for (var i = 0; i < count; i++)
    {
      var number = random.Next(4) == 0 ? random.Next(1, int.MaxValue) : random.Next(1, 500);
      long[] hits = [0, 1, random.Next(), uint.MaxValue];
      var (covered, total) = random.Next(5) switch
      {
        0 => (random.Next(0, 9), random.Next(1, 9)),
        1 => (int.MaxValue, int.MaxValue),
        2 => (3, 0),
        var _ => (0, 0),
      };

      lines[i] = new LineCoverage(number, hits[random.Next(hits.Length)], covered, total, random.Next(3) == 0, RandomMissing(random));
    }

    return lines;
  }

  private static string[]? RandomMissing(Random random)
  {
    string[] pool = [string.Empty, "0", "1:2", "é", "src/é1/0.cs"];

    return random.Next(4) switch
    {
      0 => null,
      1 => [],
      var _ => [.. Enumerable.Range(0, random.Next(1, 4)).Select(_ => pool[random.Next(pool.Length)])],
    };
  }

  // Blocks of lines run together, so they share their hits, as in a real report.
  private static FileCoverage MonorepoFile(int index, Random random)
  {
    var lines = new List<LineCoverage>();
    var number = 0;
    var blocks = random.Next(10, 25);

    for (var block = 0; block < blocks; block++)
    {
      number += random.Next(2, 12);
      var hits = random.Next(3) == 0 ? 0 : random.Next(1, 40);

      for (var line = 0; line < 8; line++)
      {
        number++;
        lines.Add(new LineCoverage(number, hits, 0, 0, line == 0, null));
      }

      number++;
      lines.Add(new LineCoverage(number, hits, random.Next(0, 3), 2, false, null));
    }

    return FileCoverage.Create($"src/module{index % 40}/feature{index % 7}/File{index}.cs", lines);
  }

  private static void AssertSameReport(CoverageReport actual, CoverageReport expected)
  {
    actual.Files.Select(file => file.Path).ShouldBe(expected.Files.Select(file => file.Path));
    actual.Totals.ShouldBe(expected.Totals);

    for (var i = 0; i < expected.Files.Count; i++)
    {
      actual.Files[i].Lines.ShouldBe(expected.Files[i].Lines);
    }
  }

  #endregion
}
