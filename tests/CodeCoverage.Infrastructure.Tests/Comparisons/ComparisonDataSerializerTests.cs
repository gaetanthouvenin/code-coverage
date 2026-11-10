// ------------------------------------------------------------------------------------------------
// <copyright file="ComparisonDataSerializerTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Text.Json;

using CodeCoverage.Domain.Comparisons;
using CodeCoverage.Infrastructure.Comparisons;

namespace CodeCoverage.Infrastructure.Tests.Comparisons;

public sealed class ComparisonDataSerializerTests
{
  #region Methods

  [Fact]
  public void RoundTrip_ShouldBeLossless()
  {
    var read = ComparisonDataSerializer.Deserialize(ComparisonDataSerializer.Serialize(ComparisonSamples.Full));

    ShouldBeTheSame(read, ComparisonSamples.Full);
    read.Delta.ShouldBe(ComparisonSamples.Full.Delta);
  }

  [Fact]
  public void RoundTrip_EmptyComparison_ShouldBeLossless()
  {
    var read = ComparisonDataSerializer.Deserialize(ComparisonDataSerializer.Serialize(ComparisonSamples.Empty));

    ShouldBeTheSame(read, ComparisonSamples.Empty);
    read.Base.ShouldBeNull();
  }

  // A delta keeps every digit: it is rounded only when it is displayed.
  [Theory]
  [InlineData("0.00001")]
  [InlineData("-99.99999")]
  [InlineData("33.33334")]
  [InlineData("0")]
  public void RoundTrip_DeltaOfAnIndirectChange_ShouldKeepEveryDigit(string text)
  {
    var delta = decimal.Parse(text, System.Globalization.CultureInfo.InvariantCulture);
    var comparison = ComparisonSamples.Empty with { IndirectChanges = [new IndirectChange("a", IndirectChangeKind.Changed, 0, 0, 0, delta)] };

    ComparisonDataSerializer.Deserialize(ComparisonDataSerializer.Serialize(comparison)).IndirectChanges[0].DeltaCoverage.ShouldBe(delta);
  }

  [Fact]
  public void Serialize_ShouldStartWithTheVersion()
  {
    ComparisonDataSerializer.Serialize(ComparisonSamples.Empty)[0].ShouldBe(ComparisonDataSerializer.CurrentVersion);
  }

  [Theory]
  [InlineData((byte)0)]
  [InlineData((byte)2)]
  [InlineData((byte)255)]
  public void Deserialize_UnknownVersion_ShouldThrowNotSupported(byte version)
  {
    var blob = ComparisonDataSerializer.Serialize(ComparisonSamples.Full);
    blob[0] = version;

    Should.Throw<NotSupportedException>(() => ComparisonDataSerializer.Deserialize(blob));
  }

  [Fact]
  public void Deserialize_Empty_ShouldThrowNotSupported()
  {
    Should.Throw<NotSupportedException>(static () => ComparisonDataSerializer.Deserialize([]));
  }

  [Fact]
  public void Deserialize_TruncatedOrForeignContent_ShouldThrowInvalidData()
  {
    var blob = ComparisonDataSerializer.Serialize(ComparisonSamples.Full);

    Should.Throw<InvalidDataException>(() => ComparisonDataSerializer.Deserialize(blob[..(blob.Length / 2)]));
    Should.Throw<InvalidDataException>(static () => ComparisonDataSerializer.Deserialize([ComparisonDataSerializer.CurrentVersion, 1, 2, 3]));
  }

  [Fact]
  public void NullArgument_ShouldThrow()
  {
    Should.Throw<ArgumentNullException>(static () => ComparisonDataSerializer.Serialize(null!));
    Should.Throw<ArgumentNullException>(static () => ComparisonDataSerializer.Deserialize(null!));
  }

  // Records compare their lists by reference: the comparison is made on what a reader sees, member by member.
  internal static void ShouldBeTheSame(Comparison actual, Comparison expected)
  {
    JsonSerializer.Serialize(actual).ShouldBe(JsonSerializer.Serialize(expected));
  }

  #endregion
}