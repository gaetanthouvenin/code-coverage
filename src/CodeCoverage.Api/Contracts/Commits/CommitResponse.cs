// ------------------------------------------------------------------------------------------------
// <copyright file="CommitResponse.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Api.Contracts.Uploads;

namespace CodeCoverage.Api.Contracts.Commits;

/// <summary>A commit, its totals and its state; with what it is made of when it is read alone.</summary>
/// <param name="Sha">The full SHA of the commit.</param>
/// <param name="Branch">The branch it was uploaded for, or <see langword="null" />.</param>
/// <param name="PullNumber">The pull request it was uploaded for, or <see langword="null" />.</param>
/// <param name="State">Where it stands: known, pending, complete…</param>
/// <param name="Totals">Its counters, or <see langword="null" /> before it is finalized.</param>
/// <param name="CompletedAt">When it was finalized, in UTC, or <see langword="null" />.</param>
/// <param name="Sessions">Its sessions, or <see langword="null" /> in a list.</param>
/// <param name="Uploads">Its uploads, or <see langword="null" /> in a list.</param>
/// <param name="Flags">The totals of its flags, or <see langword="null" /> in a list.</param>
/// <param name="Statuses">The statuses posted for it, or <see langword="null" /> in a list.</param>
public sealed record CommitResponse(
  string Sha,
  string? Branch,
  int? PullNumber,
  string State,
  CoverageTotalsModel? Totals,
  DateTimeOffset? CompletedAt,
  IReadOnlyList<SessionModel>? Sessions,
  IReadOnlyList<UploadResponse>? Uploads,
  IReadOnlyList<FlagTotalsModel>? Flags,
  IReadOnlyList<PostedStatusModel>? Statuses);
