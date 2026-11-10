// ------------------------------------------------------------------------------------------------
// <copyright file="UploadStatus.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Uploader.Http;

/// <summary>The state of an upload, as <c>GET /uploads/{id}</c> answers it: only what the uploader prints.</summary>
internal sealed record UploadStatus
{
  #region Fields

  /// <summary>The state of an upload whose coverage was stored.</summary>
  public const string Processed = "Processed";

  /// <summary>The state of an upload whose processing gave no coverage.</summary>
  public const string Failed = "Failed";

  #endregion

  #region Properties

  /// <summary>Gets the state: <c>Received</c>, <c>Processing</c>, <see cref="Processed" /> or <see cref="Failed" />.</summary>
  public string? State { get; init; }

  /// <summary>Gets the number of files of the reports whose path was found in the repository.</summary>
  public int ResolvedFiles { get; init; }

  /// <summary>Gets the number of files of the reports whose path was not found.</summary>
  public int UnresolvedFiles { get; init; }

  /// <summary>Gets what the processing reported, or <see langword="null" />.</summary>
  public UploadStatusLog? Log { get; init; }

  /// <summary>Gets why the processing failed, or <see langword="null" />.</summary>
  public string? Error { get; init; }

  #endregion
}
