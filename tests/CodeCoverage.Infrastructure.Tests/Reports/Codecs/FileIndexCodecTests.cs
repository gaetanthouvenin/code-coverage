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
      FileCoverage.Create("a.cs", [new LineCoverage(1, 1, 0, 0, false, null), new LineCoverage(2, 0, 0, 0, false, null)]),
    ]);

    var blob = _codec.EncodeFileIndex(report);

    blob[..HeaderLength].ShouldBe([(byte)'C', (byte)'F', (byte)'I', (byte)'1', 1, 0]);
    Convert.ToHexString(DecompressBody(blob)).ShouldBe("01" + "04612E6373" + "0101000000");
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
