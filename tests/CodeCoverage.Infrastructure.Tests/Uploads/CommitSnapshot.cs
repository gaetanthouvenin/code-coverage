// ------------------------------------------------------------------------------------------------
// <copyright file="CommitSnapshot.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Commits;

namespace CodeCoverage.Infrastructure.Tests.Uploads;

// Everything a finalization writes for a commit, read back from the database: the blobs as hexadecimal text, so that
// two snapshots compare by value.
public sealed record CommitSnapshot(
  Commit Commit,
  string Report,
  string FileIndex,
  IReadOnlyList<Upload> Uploads,
  IReadOnlyList<string> Coverages,
  IReadOnlyList<string> Sessions,
  IReadOnlyList<CommitFlagTotals> FlagTotals);
