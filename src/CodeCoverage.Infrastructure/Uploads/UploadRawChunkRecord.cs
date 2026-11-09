// ------------------------------------------------------------------------------------------------
// <copyright file="UploadRawChunkRecord.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Infrastructure.Uploads;

/// <summary>A piece of the gzip content of a stored part: one row of the <c>UploadRawChunks</c> table.</summary>
internal sealed class UploadRawChunkRecord
{
  #region Properties

  /// <summary>Gets or sets the identifier of the stored part.</summary>
  public long RawFileId { get; set; }

  /// <summary>Gets or sets the position of the chunk, from zero.</summary>
  public int ChunkIndex { get; set; }

  /// <summary>Gets or sets the bytes of the chunk.</summary>
  public byte[] Data { get; set; } = [];

  #endregion
}
