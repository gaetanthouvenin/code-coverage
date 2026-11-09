// ------------------------------------------------------------------------------------------------
// <copyright file="UploadCoverageRecord.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Infrastructure.Commits;

/// <summary>The coverage of a processed upload: one row of the <c>UploadCoverages</c> table.</summary>
/// <remarks>A technical record of the Infrastructure: the Application only sees the blob.</remarks>
internal sealed class UploadCoverageRecord
{
  #region Fields

  /// <summary>The value of <see cref="Codec" /> for the blobs written today.</summary>
  public const byte CurrentCodec = 1;

  #endregion

  #region Properties

  /// <summary>Gets or sets the identifier of the upload.</summary>
  public long UploadId { get; set; }

  /// <summary>Gets or sets the format of <see cref="Data" />.</summary>
  public byte Codec { get; set; } = CurrentCodec;

  /// <summary>Gets or sets the blob of the upload.</summary>
  public byte[] Data { get; set; } = [];

  #endregion
}
