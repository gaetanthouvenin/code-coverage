// ------------------------------------------------------------------------------------------------
// <copyright file="ListPullRequestsQuery.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using CodeCoverage.Application.Security;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.PullRequests;
using CodeCoverage.Domain.Users;

namespace CodeCoverage.Application.Features.PullRequests.Queries.ListPullRequests;

/// <summary>Lists one page of the pull requests of a repository, each with the comparison stored for its head.</summary>
/// <param name="Forge">The forge of the repository.</param>
/// <param name="Owner">The owner segment of the address.</param>
/// <param name="Name">The name of the repository.</param>
/// <param name="State">The state of the pull requests: <c>open</c>, <c>closed</c>, <c>merged</c> or <c>all</c>.</param>
/// <param name="Page">The number of the page, from 1.</param>
/// <param name="PageSize">The number of pull requests of a page.</param>
/// <remarks>Nothing is computed: a pull request whose head no notification compared yet comes without comparison.</remarks>
public sealed record ListPullRequestsQuery(
  Forge Forge,
  string Owner,
  string Name,
  string State = ListPullRequestsQuery.OpenState,
  int Page = 1,
  int PageSize = ListPullRequestsQuery.DefaultPageSize) : IQuery<PullRequestSummaryPage>, IRepositoryScoped
{
  #region Fields

  /// <summary>The state of the open pull requests, the one listed when none is asked.</summary>
  public const string OpenState = "open";

  /// <summary>The state of the pull requests closed without being merged.</summary>
  public const string ClosedState = "closed";

  /// <summary>The state of the merged pull requests.</summary>
  public const string MergedState = "merged";

  /// <summary>Every state.</summary>
  public const string AllStates = "all";

  /// <summary>The number of pull requests of a page when none is asked.</summary>
  public const int DefaultPageSize = 25;

  /// <summary>The largest page that can be asked.</summary>
  public const int MaxPageSize = 100;

  /// <summary>
  ///   The largest page that can be asked: its offset, <c>(page - 1) * MaxPageSize</c>, stays within an
  ///   <see cref="int" />.
  /// </summary>
  public const int MaxPage = int.MaxValue / MaxPageSize;

  #endregion

  #region Properties

  /// <inheritdoc />
  public RepositoryPermission RequiredPermission => RepositoryPermission.Read;

  #endregion

  #region Methods

  /// <summary>Reads the state asked.</summary>
  /// <param name="state">The state, as the query holds it.</param>
  /// <param name="wanted">The state of the pull requests, or <see langword="null" /> for every state.</param>
  /// <returns>Whether the text names a state, case-sensitive.</returns>
  public static bool TryParseState(string? state, out PullRequestState? wanted)
  {
    (var known, wanted) = state switch
    {
      OpenState => (true, PullRequestState.Open),
      ClosedState => (true, PullRequestState.Closed),
      MergedState => (true, PullRequestState.Merged),
      AllStates => (true, (PullRequestState?)null),
      var _ => (false, (PullRequestState?)null),
    };

    return known;
  }

  #endregion
}
