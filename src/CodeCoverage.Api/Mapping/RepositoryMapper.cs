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
  /// <param name="cancellationToken">A token to cancel the request.</param>
  /// <returns>The repositories.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="sender" /> is <see langword="null" />.</exception>
  public static async Task<IReadOnlyList<RepositoryResponse>> ListRepositoriesResponseAsync(
    this ISender sender,
    string forge,
    string organization,
    string? search,
    CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(sender);

    var parsed = AddressSegments.ToOrganizationForge(forge);
    var repositories = await sender.Send(new ListRepositoriesQuery(parsed, organization, search), cancellationToken).ConfigureAwait(false);

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
      repository.IsPrivate
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
