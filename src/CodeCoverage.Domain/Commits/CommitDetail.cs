// ------------------------------------------------------------------------------------------------
// <copyright file="CommitDetail.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Notifications;

namespace CodeCoverage.Domain.Commits;

/// <summary>A commit with what it is made of: its sessions, its uploads, the totals of its flags and its statuses.</summary>
/// <param name="Commit">The commit, with its totals and state.</param>
/// <param name="Sessions">Its sessions, by number.</param>
/// <param name="Uploads">Its uploads, by number.</param>
/// <param name="Flags">The totals of each of its flags, carried forward ones included.</param>
/// <param name="Statuses">The statuses posted for it.</param>
/// <remarks>A read model, for the API and the pages: it never decodes a report.</remarks>
public sealed record CommitDetail(
  Commit Commit,
  IReadOnlyList<Session> Sessions,
  IReadOnlyList<Upload> Uploads,
  IReadOnlyList<CommitFlagTotals> Flags,
  IReadOnlyList<PostedStatus> Statuses);
