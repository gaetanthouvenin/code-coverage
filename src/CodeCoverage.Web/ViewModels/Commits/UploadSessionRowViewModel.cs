// ------------------------------------------------------------------------------------------------
// <copyright file="UploadSessionRowViewModel.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Web.ViewModels.Commits;

/// <summary>A row of the uploads and sessions of a commit: an upload of the commit, or a session carried forward.</summary>
/// <param name="Key">The key of the row, stable across renders.</param>
/// <param name="Number">The sequence of the upload, or the number of the carried session.</param>
/// <param name="Name">The name given by the uploader, or <see langword="null" />.</param>
/// <param name="Status">The state of the row.</param>
/// <param name="UploadId">The public identifier of the upload, or <see langword="null" /> for a carried session.</param>
/// <param name="Ci">The CI service and build, e.g. <c>GitHub Actions #8812</c>, or <see langword="null" />.</param>
/// <param name="CiUrl">The http or https address of the build, or <see langword="null" />.</param>
/// <param name="Flags">The flags of the upload or session.</param>
/// <param name="Files">The number of files resolved, formatted, or <see langword="null" /> when unknown.</param>
/// <param name="ResolvedFiles">The number of files resolved, for the log line of a processed upload.</param>
/// <param name="CarriedFrom">The short SHA of the commit a carried session comes from, or <see langword="null" />.</param>
/// <param name="ErrorCode">The error code of a failed upload, or <see langword="null" />.</param>
/// <param name="Log">The processing log.</param>
public sealed record UploadSessionRowViewModel(
  string Key,
  int Number,
  string? Name,
  UploadStatus Status,
  Guid? UploadId,
  string? Ci,
  string? CiUrl,
  IReadOnlyList<string> Flags,
  string? Files,
  int ResolvedFiles,
  string? CarriedFrom,
  string? ErrorCode,
  ProcessingLogViewModel Log);
