// ------------------------------------------------------------------------------------------------
// <copyright file="UploadRawFileRecord.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Contracts.Uploads;
using CodeCoverage.Domain.Coverage;

namespace CodeCoverage.Infrastructure.Uploads;

/// <summary>A stored part of an upload: one row of the <c>UploadRawFiles</c> table.</summary>
/// <remarks>A technical record of the Infrastructure, seen by the Application as an <see cref="UploadPart" />.</remarks>
internal sealed class UploadRawFileRecord
{
  #region Fields

  /// <summary>The length of <see cref="Sha256" />.</summary>
  public const int Sha256Length = 32;

  #endregion

  #region Properties

  /// <summary>Gets or sets the identifier of the row, assigned when it is saved.</summary>
  public long Id { get; set; }

  /// <summary>Gets or sets the public identifier of the upload.</summary>
  public Guid UploadPublicId { get; set; }

  /// <summary>Gets or sets what the part holds.</summary>
  public UploadPartKind Kind { get; set; }

  /// <summary>Gets or sets the name the part was sent under.</summary>
  public string FileName { get; set; } = string.Empty;

  /// <summary>Gets or sets the format found in the report, or <see langword="null" />.</summary>
  public ReportFormat? DetectedFormat { get; set; }

  /// <summary>Gets or sets the SHA-256 of the decompressed content, all zero until it is written.</summary>
  public byte[] Sha256 { get; set; } = new byte[Sha256Length];

  /// <summary>Gets or sets the number of bytes stored.</summary>
  public long CompressedSize { get; set; }

  /// <summary>Gets or sets the number of bytes of the decompressed content.</summary>
  public long UncompressedSize { get; set; }

  /// <summary>Gets or sets when the row can be purged, in UTC.</summary>
  public DateTimeOffset ExpiresAt { get; set; }

  #endregion
}
