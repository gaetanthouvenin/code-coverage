// ------------------------------------------------------------------------------------------------
// <copyright file="CommitDetail.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Comparisons;
using CodeCoverage.Domain.Config;
using CodeCoverage.Domain.Notifications;

namespace CodeCoverage.Domain.Commits;

/// <summary>A commit with what it is made of: its sessions, its uploads, the totals of its flags and its statuses.</summary>
/// <param name="Commit">The commit, with its totals and state.</param>
/// <param name="Sessions">Its sessions, by number.</param>
/// <param name="Uploads">Its uploads, by number.</param>
/// <param name="Flags">The totals of each of its flags, carried forward ones included.</param>
/// <param name="Statuses">The statuses posted for it.</param>
/// <param name="Git">Its message, author and date from the cache of the forge, or <see langword="null" /> when not cached.</param>
/// <param name="Config">The configuration stored for it: source, path, warnings and errors really applied, or <see langword="null" />.</param>
/// <param name="Comparison">Its stored push comparison (against its parent on its branch), or <see langword="null" />.</param>
/// <param name="BaseFiles">The number of files of the base of that comparison, or <see langword="null" /> without a base.</param>
/// <remarks>A read model, for the API and the pages: it never decodes a report.</remarks>
public sealed record CommitDetail(
  Commit Commit,
  IReadOnlyList<Session> Sessions,
  IReadOnlyList<Upload> Uploads,
  IReadOnlyList<CommitFlagTotals> Flags,
  IReadOnlyList<PostedStatus> Statuses,
  GitCommit? Git = null,
  CommitConfig? Config = null,
  CommitComparison? Comparison = null,
  int? BaseFiles = null);
