// ------------------------------------------------------------------------------------------------
// <copyright file="FileIndexCodecTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.IO.Compression;

using CodeCoverage.Domain.Coverage;
using CodeCoverage.Infrastructure.Reports.Codecs;

namespace CodeCoverage.Infrastructure.Tests.Reports.Codecs;

public sealed class FileIndexCodecTests
{
  #region Fields

  private const int HeaderLength = 6;
  private const int VersionOffset = 4;

  private readonly CcrCodec _codec = new();

  #endregion

  #region Methods

  [Fact]
  public void RoundTrip_Report_ShouldGiveTheCountersOfEachFile()
  {
    var report = SampleReport();

    var index = _codec.DecodeFileIndex(_codec.EncodeFileIndex(report));

    index.ShouldBe(report.FileTotals);
    index[1].Totals.Lines.ShouldBe(3);
  }

  [Fact]
  public void RoundTrip_EmptyReport_ShouldGiveNoFile()
  {
    _codec.DecodeFileIndex(_codec.EncodeFileIndex(CoverageReport.Create([]))).ShouldBeEmpty();
  }

  [Fact]
  public void Encode_ShouldWriteTheFrozenHeaderAndBody()
  {
    var report = CoverageReport.Create(
    [
      FileCoverage.Create("a.cs", [new LineCoverage(1, 1, 0, 0, false, null), new LineCoverage(2, 0, 1, 2, false, null)]),
      FileCoverage.Create("b.cs", [new LineCoverage(1, 0, 0, 0, false, null)], [new MethodCoverage(1, 0), new MethodCoverage(4, 2)]),
    ]);

    var blob = _codec.EncodeFileIndex(report);

    blob[..HeaderLength].ShouldBe([(byte)'C', (byte)'F', (byte)'I', (byte)'2', 1, 0]);
    Convert.ToHexString(DecompressBody(blob))
           .ShouldBe("02" + "04612E6373" + "0100010100" + "010102" + "00" + "04622E6373" + "0001000000" + "000000" + "01" + "0102");
  }

  [Fact]
  public void Decode_Cfi1_ShouldHaveNoDetail()
  {
    var blob = CcrCodec.Seal("CFI1"u8, Convert.FromHexString("01" + "04612E6373" + "0101000000"));

    var file = _codec.DecodeFileIndex(blob).ShouldHaveSingleItem();

    file.ShouldBe(new FileTotals("a.cs", new CoverageTotals(1, 1, 0, 0, 0)));
    file.Detail.ShouldBeNull();
  }

  [Fact]
  public void RoundTrip_Cfi2_ShouldKeepTheDetail()
  {
    var report = CoverageReport.Create(
    [
      FileCoverage.Create("a.cs", [new LineCoverage(1, 1, 0, 0, false, null)], [new MethodCoverage(1, 1)]),
      FileCoverage.Create("b.cs", [new LineCoverage(1, 2, 0, 2, false, null)]),
    ]);

    var index = _codec.DecodeFileIndex(_codec.EncodeFileIndex(report));

    index[0].Detail.ShouldBe(new CoverageDetail(1, 1, 0, 0, 1, 1));
    index[1].Detail.ShouldBe(new CoverageDetail(1, 1, 0, 2, null, null));
  }

  [Fact]
  public void Encode_NullReport_ShouldThrow()
  {
    Should.Throw<ArgumentNullException>(() => _codec.EncodeFileIndex(null!));
  }

  [Fact]
  public void Decode_UnknownVersion_ShouldThrow()
  {
    var blob = _codec.EncodeFileIndex(SampleReport());
    blob[VersionOffset] = 2;

    Should.Throw<InvalidDataException>(() => _codec.DecodeFileIndex(blob)).Message.ShouldContain("not supported");
  }

  [Fact]
  public void Decode_ReportBlob_ShouldThrow()
  {
    var blob = _codec.Encode(SampleReport());

    Should.Throw<InvalidDataException>(() => _codec.DecodeFileIndex(blob)).Message.ShouldContain("magic");
  }

  [Fact]
  public void Decode_EveryTruncation_ShouldThrow()
  {
    var blob = _codec.EncodeFileIndex(SampleReport());

    for (var length = 0; length < blob.Length; length++)
    {
      var truncated = blob[..length];

      Should.Throw<InvalidDataException>(() => _codec.DecodeFileIndex(truncated), $"length {length}");
    }
  }

  [Theory]
  [InlineData("FFFFFFFF07", "count exceeds")]
  [InlineData("01" + "0120" + "0000000000", "blank")]
  [InlineData("01" + "0161" + "FFFFFFFF07" + "FFFFFFFF07" + "00" + "0000", "number of lines")]
  [InlineData("01" + "0161" + "FFFFFFFF0F" + "00000000", "out of range")]
  [InlineData("02" + "0162" + "0000000000" + "0161" + "0000000000", "sorted and distinct")]
  [InlineData("02" + "0161" + "0000000000" + "0161" + "0000000000", "sorted and distinct")]
  [InlineData("01" + "0161" + "0000000000" + "00", "unexpected bytes")]
  public void Decode_CorruptedBody_ShouldThrowAClearError(string body, string reason)
  {
    var blob = CcrCodec.Seal("CFI1"u8, Convert.FromHexString(body));

    Should.Throw<InvalidDataException>(() => _codec.DecodeFileIndex(blob)).Message.ShouldContain(reason);
  }

  [Theory]
  [InlineData("01" + "0161" + "0100000000" + "02" + "0000" + "00", "more executed lines")]
  [InlineData("01" + "0161" + "0100000000" + "01" + "0302" + "00", "more covered branches")]
  [InlineData("01" + "0161" + "0100000000" + "01" + "0000" + "02", "unknown flags")]
  [InlineData("01" + "0161" + "0100000000" + "01" + "0000" + "01" + "0302", "more executed methods")]
  [InlineData("01" + "0161" + "0100000000" + "01" + "0000" + "01" + "00", "ends too early")]
  [InlineData("01" + "0161" + "0100000000" + "01" + "0000" + "00" + "00", "unexpected bytes")]
  public void Decode_CorruptedCfi2Body_ShouldThrowAClearError(string body, string reason)
  {
    var blob = CcrCodec.Seal("CFI2"u8, Convert.FromHexString(body));

    Should.Throw<InvalidDataException>(() => _codec.DecodeFileIndex(blob)).Message.ShouldContain(reason);
  }

  private static byte[] DecompressBody(byte[] blob)
  {
    using var input = new MemoryStream(blob, HeaderLength, blob.Length - HeaderLength);
    using var brotli = new BrotliStream(input, CompressionMode.Decompress);
    using var output = new MemoryStream();

    brotli.CopyTo(output);

    return output.ToArray();
  }

  private static CoverageReport SampleReport() =>
    CoverageReport.Create(
    [
      FileCoverage.Create("src/a.cs", [new LineCoverage(1, 1, 0, 0, false, null)]),
      FileCoverage.Create(
        "src/é.cs",
        [
          new LineCoverage(1, 0, 0, 0, true, null),
          new LineCoverage(2, 1, 1, 2, false, null),
          new LineCoverage(3, 1, 2, 2, false, []),
        ]),
    ]);

  #endregion
}
