// ------------------------------------------------------------------------------------------------
// <copyright file="RepositoryMapper.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using CodeCoverage.Api.Contracts.Commits;
using CodeCoverage.Api.Contracts.Configuration;
using CodeCoverage.Api.Contracts.Repositories;
using CodeCoverage.Application.Exceptions;
using CodeCoverage.Application.Features.Organizations;
using CodeCoverage.Application.Features.Repositories;
using CodeCoverage.Application.Features.Repositories.Commands.ActivateRepository;
using CodeCoverage.Application.Features.Repositories.Commands.DeactivateRepository;
using CodeCoverage.Application.Features.Repositories.Queries.GetCoverageHistory;
using CodeCoverage.Application.Features.Repositories.Queries.GetEffectiveConfig;
using CodeCoverage.Application.Features.Repositories.Queries.GetRepository;
using CodeCoverage.Application.Features.Repositories.Queries.ListBranches;
using CodeCoverage.Application.Features.Repositories.Queries.ListRepositories;
using CodeCoverage.Domain.Commits;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Repositories;

namespace CodeCoverage.Api.Mapping;

/// <summary>Converts the repositories between the Domain and the API.</summary>
internal static class RepositoryMapper
{
  #region Methods

  /// <summary>Lists the repositories of an organization the user may read and converts them for the API.</summary>
  /// <param name="sender">The request sender.</param>
  /// <param name="forge">The forge segment of the address.</param>
  /// <param name="organization">The login of the organization.</param>
  /// <param name="search">A text the name must contain, or <see langword="null" />.</param>
  /// <param name="active">The state of the repositories listed, or <see langword="null" /> for both.</param>
  /// <param name="cancellationToken">A token to cancel the request.</param>
  /// <returns>The repositories.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="sender" /> is <see langword="null" />.</exception>
  public static async Task<IReadOnlyList<RepositoryResponse>> ListRepositoriesResponseAsync(
    this ISender sender,
    string forge,
    string organization,
    string? search,
    bool? active,
    CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(sender);

    var parsed = AddressSegments.ToOrganizationForge(forge);
    var repositories = await sender.Send(new ListRepositoriesQuery(parsed, organization, search, active), cancellationToken).ConfigureAwait(false);

    return [.. repositories.Select(r => ToResponse(r, parsed))];
  }

  /// <summary>Reads a repository and converts it for the API.</summary>
  /// <param name="sender">The request sender.</param>
  /// <param name="forge">The forge segment of the address.</param>
  /// <param name="owner">The owner segment of the address.</param>
  /// <param name="repo">The name of the repository.</param>
  /// <param name="cancellationToken">A token to cancel the request.</param>
  /// <returns>The repository, with its branches, its flags and its last totals.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="sender" /> is <see langword="null" />.</exception>
  public static async Task<RepositoryDetailResponse> GetRepositoryResponseAsync(
    this ISender sender,
    string forge,
    string owner,
    string repo,
    CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(sender);

    var parsed = AddressSegments.ToRepositoryForge(forge);
    var overview = await sender.Send(new GetRepositoryQuery(parsed, owner, repo), cancellationToken).ConfigureAwait(false);

    return new RepositoryDetailResponse(
      ToResponse(overview.Repository, parsed),
      [.. overview.Branches.Select(ToResponse)],
      overview.Flags,
      [.. overview.Totals.Select(ToModel)]
    );
  }

  /// <summary>Lists the branches of a repository and converts them for the API.</summary>
  /// <param name="sender">The request sender.</param>
  /// <param name="forge">The forge segment of the address.</param>
  /// <param name="owner">The owner segment of the address.</param>
  /// <param name="repo">The name of the repository.</param>
  /// <param name="cancellationToken">A token to cancel the request.</param>
  /// <returns>The branches, by name.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="sender" /> is <see langword="null" />.</exception>
  public static async Task<IReadOnlyList<BranchResponse>> ListBranchesResponseAsync(
    this ISender sender,
    string forge,
    string owner,
    string repo,
    CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(sender);

    var branches = await sender.Send(new ListBranchesQuery(AddressSegments.ToRepositoryForge(forge), owner, repo), cancellationToken).ConfigureAwait(false);

    return [.. branches.Select(ToResponse)];
  }

  /// <summary>Enables a repository an administrator disabled.</summary>
  /// <param name="sender">The request sender.</param>
  /// <param name="forge">The forge segment of the address.</param>
  /// <param name="owner">The owner segment of the address.</param>
  /// <param name="repo">The name of the repository.</param>
  /// <param name="cancellationToken">A token to cancel the request.</param>
  /// <returns>A task that completes when the repository is enabled.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="sender" /> is <see langword="null" />.</exception>
  public static async Task ActivateRepositoryAsync(this ISender sender, string forge, string owner, string repo, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(sender);

    await sender.Send(new ActivateRepositoryCommand(AddressSegments.ToRepositoryForge(forge), owner, repo), cancellationToken).ConfigureAwait(false);
  }

  /// <summary>Disables a repository.</summary>
  /// <param name="sender">The request sender.</param>
  /// <param name="forge">The forge segment of the address.</param>
  /// <param name="owner">The owner segment of the address.</param>
  /// <param name="repo">The name of the repository.</param>
  /// <param name="cancellationToken">A token to cancel the request.</param>
  /// <returns>A task that completes when the repository is disabled.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="sender" /> is <see langword="null" />.</exception>
  public static async Task DeactivateRepositoryAsync(this ISender sender, string forge, string owner, string repo, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(sender);

    await sender.Send(new DeactivateRepositoryCommand(AddressSegments.ToRepositoryForge(forge), owner, repo), cancellationToken).ConfigureAwait(false);
  }

  /// <summary>Reads the coverage history of a branch and converts it for the API.</summary>
  /// <param name="sender">The request sender.</param>
  /// <param name="forge">The forge segment of the address.</param>
  /// <param name="owner">The owner segment of the address.</param>
  /// <param name="repo">The name of the repository.</param>
  /// <param name="branch">The branch, or <see langword="null" /> for the default branch.</param>
  /// <param name="flags">The flags, or none for the whole project.</param>
  /// <param name="from">The first instant, or <see langword="null" />.</param>
  /// <param name="to">The last instant, or <see langword="null" />.</param>
  /// <param name="interval">The grouping, or <see langword="null" /> for one point per commit.</param>
  /// <param name="cancellationToken">A token to cancel the request.</param>
  /// <returns>One series per flag, in the order asked.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="sender" /> is <see langword="null" />.</exception>
  public static async Task<IReadOnlyList<CoverageSeriesResponse>> GetCoverageHistoryResponseAsync(
    this ISender sender,
    string forge,
    string owner,
    string repo,
    string? branch,
    IReadOnlyList<string>? flags,
    DateTimeOffset? from,
    DateTimeOffset? to,
    string? interval,
    CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(sender);

    var query = new GetCoverageHistoryQuery(
      AddressSegments.ToRepositoryForge(forge),
      owner,
      repo,
      branch,
      flags ?? [],
      from,
      to,
      interval ?? GetCoverageHistoryQuery.CommitInterval
    );
    var series = await sender.Send(query, cancellationToken).ConfigureAwait(false);

    return
    [
      .. series.Select(static s => new CoverageSeriesResponse(
                         s.Flag.Length == 0 ? null : s.Flag,
                         [.. s.Points.Select(static p => new CoveragePointModel(p.At, p.Sha.Value, p.Lines, p.Hits, p.Misses, p.Partials, p.Coverage))])),
    ];
  }

  /// <summary>Reads the effective configuration of a repository and converts it for the API.</summary>
  /// <param name="sender">The request sender.</param>
  /// <param name="forge">The forge segment of the address.</param>
  /// <param name="owner">The owner segment of the address.</param>
  /// <param name="repo">The name of the repository.</param>
  /// <param name="cancellationToken">A token to cancel the request.</param>
  /// <returns>The configuration, merged as JSON, with its source and the YAML found.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="sender" /> is <see langword="null" />.</exception>
  public static async Task<EffectiveConfigResponse> GetEffectiveConfigResponseAsync(
    this ISender sender,
    string forge,
    string owner,
    string repo,
    CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(sender);

    var effective = await sender.Send(new GetEffectiveConfigQuery(AddressSegments.ToRepositoryForge(forge), owner, repo), cancellationToken)
                                .ConfigureAwait(false);

    return new EffectiveConfigResponse(
      effective.Source.ToString(),
      effective.Sha?.Value,
      effective.IsResolvedAtSha,
      effective.Path,
      effective.Yaml,
      ConfigNodeJson.ToJsonElement(effective.Config.Root),
      [.. effective.Warnings.Select(static w => new ConfigWarningModel(w.Code, w.Key, w.Detail))],
      effective.Errors
    );
  }

  private static RepositoryResponse ToResponse(TrackedRepository repository, Forge forge) =>
    new(
      ForgeSegments.ToSegment(forge),
      repository.Namespace.Length == 0 ? repository.Owner : $"{repository.Owner}:{repository.Namespace.Replace('/', ':')}",
      repository.Name,
      repository.DefaultBranch,
      repository.IsPrivate,
      repository.DisabledAt is null
    );

  private static BranchResponse ToResponse(Branch branch) => new(branch.Name, branch.HeadSha.Value, branch.HeadUpdatedAt, branch.CoveredAt);

  private static FlagTotalsModel ToModel(CommitFlagTotals totals) =>
    new(
      totals.Flag,
      new CoverageTotalsModel(totals.Lines, totals.Hits, totals.Misses, totals.Partials, totals.Coverage),
      totals.IsCarriedForward,
      totals.CarriedForwardFromSha?.Value
    );

  #endregion
}
