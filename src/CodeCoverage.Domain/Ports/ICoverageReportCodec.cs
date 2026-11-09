// ------------------------------------------------------------------------------------------------
// <copyright file="ICoverageReportCodec.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Coverage;

namespace CodeCoverage.Domain.Ports;

/// <summary>Turns coverage reports into the compact, versioned blobs kept in the database.</summary>
public interface ICoverageReportCodec
{
  #region Methods

  /// <summary>Encodes a report.</summary>
  /// <param name="report">The report.</param>
  /// <returns>The blob.</returns>
  /// <remarks>
  ///   The hits of a line are kept within the range of an unsigned 32-bit integer, which never
  ///   changes the state of the line; everything else is kept as it is.
  /// </remarks>
  /// <exception cref="ArgumentNullException">
  ///   <paramref name="report" /> is <see langword="null" />.
  /// </exception>
  /// <exception cref="ArgumentOutOfRangeException">A branch counter of a line is negative.</exception>
  byte[] Encode(CoverageReport report);

  /// <summary>Decodes a blob written by <see cref="Encode" />.</summary>
  /// <param name="blob">The blob.</param>
  /// <returns>The report.</returns>
  /// <exception cref="InvalidDataException">
  ///   The blob is truncated or corrupted, or its version is not supported.
  /// </exception>
  CoverageReport Decode(ReadOnlyMemory<byte> blob);

  /// <summary>Encodes the counters of each file of a report, without its lines.</summary>
  /// <param name="report">The report.</param>
  /// <returns>The blob.</returns>
  /// <exception cref="ArgumentNullException">
  ///   <paramref name="report" /> is <see langword="null" />.
  /// </exception>
  byte[] EncodeFileIndex(CoverageReport report);

  /// <summary>Decodes a blob written by <see cref="EncodeFileIndex" />.</summary>
  /// <param name="blob">The blob.</param>
  /// <returns>The counters of each file, in ordinal order of their paths.</returns>
  /// <exception cref="InvalidDataException">
  ///   The blob is truncated or corrupted, or its version is not supported.
  /// </exception>
  IReadOnlyList<FileTotals> DecodeFileIndex(ReadOnlyMemory<byte> blob);

  #endregion
}
