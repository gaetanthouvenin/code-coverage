// ------------------------------------------------------------------------------------------------
// <copyright file="ListRepositorySummariesQuery.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using CodeCoverage.Application.Security;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Repositories;

namespace CodeCoverage.Application.Features.Repositories.Queries.ListRepositorySummaries;

/// <summary>
///   Lists the repositories of an organization that the user can read, each with the covered head of its default branch,
///   its flags and its daily coverage over the last 30 days, in a few grouped reads: never one request per repository.
/// </summary>
/// <param name="Forge">The forge of the organization.</param>
/// <param name="Login">The login of the organization.</param>
/// <param name="Search">A part of the name to look for, or <see langword="null" />.</param>
/// <param name="Active">
///   <see langword="true" /> for the repositories that are not disabled, <see langword="false" /> for the disabled ones,
///   <see langword="null" /> for both.
/// </param>
public sealed record ListRepositorySummariesQuery(Forge Forge, string Login, string? Search, bool? Active = null)
  : IQuery<IReadOnlyList<RepositorySummary>>, IOrganizationScoped
{
  #region Fields

  /// <summary>The number of days of the daily history of each repository.</summary>
  public const int HistoryDays = 30;

  /// <summary>The number of project totals read at most for the histories of the whole list, the most recent kept.</summary>
  public const int MaxHistoryRows = 10_000;

  #endregion

  #region Properties

  /// <inheritdoc />
  public OrganizationRole RequiredRole => OrganizationRole.Participant;

  #endregion
}
