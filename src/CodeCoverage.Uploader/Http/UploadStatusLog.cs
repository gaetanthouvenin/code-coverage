// ------------------------------------------------------------------------------------------------
// <copyright file="UploadStatusLog.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Uploader.Http;

/// <summary>The processing log of an upload, as the server answers it.</summary>
internal sealed record UploadStatusLog
{
  #region Properties

  /// <summary>Gets the messages the server kept, or <see langword="null" />.</summary>
  public IReadOnlyList<string>? Messages { get; init; }

  /// <summary>Gets the number of messages the processing wrote, kept or not.</summary>
  public int MessageTotal { get; init; }

  /// <summary>Gets the unresolved or ambiguous paths the server kept, or <see langword="null" />.</summary>
  public IReadOnlyList<string>? Paths { get; init; }

  /// <summary>Gets the number of unresolved or ambiguous paths, kept or not.</summary>
  public int PathTotal { get; init; }

  #endregion
}
