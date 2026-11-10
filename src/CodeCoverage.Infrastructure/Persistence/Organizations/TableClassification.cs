// ------------------------------------------------------------------------------------------------
// <copyright file="TableClassification.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Collections.Frozen;

namespace CodeCoverage.Infrastructure.Persistence.Organizations;

/// <summary>The class of every table of the model (ADR-013).</summary>
/// <remarks>
///   A test requires each table of the model to be in exactly one of these sets, and only tables of the
///   model: a new table does not build until its author has decided what it holds.
/// </remarks>
internal static class TableClassification
{
  #region Properties

  /// <summary>Gets the tables without business data, read before an organization is entered.</summary>
  public static IReadOnlySet<string> Global { get; } =
    new[] { "DataProtectionKeys", "Installations", "Organizations", "Settings", "SettingsRevision", "Users" }.ToFrozenSet(StringComparer.Ordinal);

  /// <summary>Gets the tables of the rows of one organization, under the query filter.</summary>
  public static IReadOnlySet<string> OrganizationOwned { get; } =
    new[]
    {
      "Branches", "CommitConfigs", "CommitFlagTotals", "CommitReports", "CommitSessions", "Commits", "Comparisons", "DiffCache", "GitCommits", "OrganizationSettings", "PullRequests", "Repositories",
      "RepositoryTrees", "UploadCoverages", "UploadRawChunks", "UploadRawFiles", "UploadTokens", "Uploads", "UserRepositoryAccess",
    }.ToFrozenSet(StringComparer.Ordinal);

  /// <summary>Gets the operational tables, which are never filtered.</summary>
  public static IReadOnlySet<string> Operational { get; } =
    new[] { "AuditEntries", "Jobs", "WebhookDeliveries" }.ToFrozenSet(StringComparer.Ordinal);

  #endregion
}
