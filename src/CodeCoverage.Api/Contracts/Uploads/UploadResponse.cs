// ------------------------------------------------------------------------------------------------
// <copyright file="UploadResponse.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Api.Contracts.Uploads;

/// <summary>The state of an upload and what its processing reported.</summary>
/// <param name="Id">The public identifier of the upload.</param>
/// <param name="State">Where it stands in its processing.</param>
/// <param name="Flags">Its flags.</param>
/// <param name="Name">The name of the session, or <see langword="null" />.</param>
/// <param name="ResolvedFiles">The number of files whose path was resolved.</param>
/// <param name="UnresolvedFiles">The number of files whose path could not be resolved.</param>
/// <param name="Totals">Its counters, or <see langword="null" /> before it is processed.</param>
/// <param name="Log">What its processing reported.</param>
/// <param name="Error">The code of its failure, or <see langword="null" />.</param>
public sealed record UploadResponse(
  Guid Id,
  string State,
  IReadOnlyList<string> Flags,
  string? Name,
  int ResolvedFiles,
  int UnresolvedFiles,
  UploadTotalsModel? Totals,
  UploadLogModel Log,
  string? Error);
