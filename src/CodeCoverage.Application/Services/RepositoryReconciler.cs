// ------------------------------------------------------------------------------------------------
// <copyright file="RepositoryReconciler.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Globalization;

using CodeCoverage.Domain.Contracts.Forges;
using CodeCoverage.Domain.Contracts.Jobs;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Domain.Repositories;

namespace CodeCoverage.Application.Services;

/// <summary>
///   What the handling of a webhook delivery and the reconciliation of an installation share: bringing the
///   tracked repositories of the organization of the scope in line with what GitHub reports.
/// </summary>
/// <param name="repositories">The repository of the tracked repositories.</param>
/// <param name="directory">The directory of the organizations.</param>
/// <param name="gateways">The gateways of the forges.</param>
/// <param name="tokenGenerator">The generator of the badge tokens.</param>
/// <param name="secretProtector">What protects a badge token before it is stored.</param>
/// <param name="auditLog">The audit log.</param>
/// <param name="clock">The clock.</param>
internal sealed class RepositoryReconciler(
  IRepositoryRepository repositories,
  IOrganizationDirectory directory,
  IForgeGatewayProvider gateways,
  ITokenGenerator tokenGenerator,
  ISecretProtector secretProtector,
  IAuditLog auditLog,
  IClock clock)
{
  #region Fields

  /// <summary>The audit action of the revocation of the upload tokens of a repository that left.</summary>
  internal const string RevokeTokensAuditAction = "Repositories.Transfer.RevokeUploadTokens";

  /// <summary>The audit action of the replacement of the badge token of a repository that left.</summary>
  internal const string RotateBadgeTokenAuditAction = "Repositories.Transfer.RotateBadgeToken";

  /// <summary>The audit action of the advice to replace the upload tokens of the organization.</summary>
  internal const string AdviseRotationAuditAction = "Repositories.Transfer.AdviseTokenRotation";

  #endregion

  #region Methods

  /// <summary>Builds the row of a repository that GitHub reports, with a new badge token.</summary>
  /// <param name="installationId">The identifier of the installation that serves the repository.</param>
  /// <param name="info">What GitHub reports.</param>
  /// <returns>The repository, not saved yet.</returns>
  public TrackedRepository Create(long installationId, ForgeRepositoryInfo info)
  {
    ArgumentNullException.ThrowIfNull(info);

    return TrackedRepository.Create(
      installationId,
      info.ExternalId,
      RepositorySlug.Create(info.Owner, info.Namespace, info.Name),
      info.DefaultBranch,
      info.IsPrivate,
      secretProtector.Protect(tokenGenerator.NewBadgeToken()),
      clock.UtcNow
    );
  }

  /// <summary>Applies to a tracked repository what GitHub reports: name, visibility and default branch.</summary>
  /// <param name="repository">The repository, read in this scope.</param>
  /// <param name="info">What GitHub reports.</param>
  /// <returns><see langword="true" /> when the repository changed and must be saved.</returns>
  public bool Apply(TrackedRepository repository, ForgeRepositoryInfo info)
  {
    ArgumentNullException.ThrowIfNull(repository);
    ArgumentNullException.ThrowIfNull(info);

    var version = repository.Version;
    var now = clock.UtcNow;

    repository.Rename(RepositorySlug.Create(info.Owner, info.Namespace, info.Name), now);
    repository.ChangeVisibility(info.IsPrivate, now);

    // The body of an `installation_repositories` delivery does not carry the default branch.
    if (info.DefaultBranch.Length > 0)
    {
      repository.ChangeDefaultBranch(info.DefaultBranch, now);
    }

    return repository.Version != version;
  }

  /// <summary>
  ///   Closes the row of a repository that was transferred to another account: it is deactivated, its upload
  ///   tokens are revoked, its badge token is replaced and the tokens of the organization are marked as to
  ///   be replaced.
  /// </summary>
  /// <param name="repository">The repository, read in this scope.</param>
  /// <param name="cancellationToken">A token to cancel the work.</param>
  /// <returns>
  ///   <see langword="true" /> when the row was closed; <see langword="false" /> when GitHub still reports the
  ///   repository under the account of the organization, which then keeps it as it is.
  /// </returns>
  /// <remarks>
  ///   The forge is asked again because the request can be late: a repository transferred away and back
  ///   before this runs still belongs to the organization. No data moves (ADR-013): the history stays here.
  /// </remarks>
  public async Task<bool> CloseTransferredAsync(TrackedRepository repository, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(repository);

    var organization = await directory.FindCurrentAsync(cancellationToken).ConfigureAwait(false)
                       ?? throw new InvalidOperationException("The scope has not entered an organization.");

    var reported = await gateways.GetGateway(organization.Forge)
                                 .GetRepositoryAsync(repository.ToForgeRef(organization.Forge), cancellationToken)
                                 .ConfigureAwait(false);

    if (reported is not null && IsOwnedBy(reported, organization))
    {
      return false;
    }

    var now = clock.UtcNow;

    // Closed once: a request that comes again, from a retry or from the other side of the transfer, finds the row
    // closed and replaces nothing more. A row that is only inactive is not closed: the installation that stopped
    // serving the repository, before the transfer was known here, deactivated it without touching a token.
    var closing = repository.TransferredAt is null;

    if (closing)
    {
      repository.CloseAsTransferred(secretProtector.Protect(tokenGenerator.NewBadgeToken()), now);
    }

    var tokens = await repositories.SaveDepartedAsync(repository, now, cancellationToken).ConfigureAwait(false);
    var target = repository.Id.ToString(CultureInfo.InvariantCulture);

    if (tokens.Revoked > 0)
    {
      await auditLog.RecordAsync(RevokeTokensAuditAction, target, Count(tokens.Revoked), cancellationToken).ConfigureAwait(false);
    }

    if (closing)
    {
      await auditLog.RecordAsync(RotateBadgeTokenAuditAction, target, details: null, cancellationToken).ConfigureAwait(false);
    }

    if (tokens.RotationAdvised > 0)
    {
      await auditLog.RecordAsync(AdviseRotationAuditAction, target, Count(tokens.RotationAdvised), cancellationToken).ConfigureAwait(false);
    }

    return true;
  }

  /// <summary>
  ///   Saves a repository GitHub reports, new or changed, after freeing its name when a row that is no longer
  ///   tracked still holds it.
  /// </summary>
  /// <param name="repository">The repository: new, or read in this scope and changed.</param>
  /// <param name="isNew">Whether the repository has no row yet.</param>
  /// <param name="job">The job to queue in the transaction of the save, or <see langword="null" />.</param>
  /// <param name="cancellationToken">A token to cancel the saving.</param>
  /// <returns>A task that completes when the repository is saved.</returns>
  /// <remarks>
  ///   A repository deleted or transferred away keeps its row, and the name is unique in the organization:
  ///   without this, a repository created or renamed under that name could never be saved. A name held by a
  ///   row that is still tracked is left to the forge, which renames that row in its turn.
  /// </remarks>
  public async Task SaveAsync(TrackedRepository repository, bool isNew, JobRequest? job, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(repository);

    var slug = RepositorySlug.Create(repository.Owner, repository.Namespace, repository.Name);
    var holder = await repositories.FindBySlugAsync(slug, cancellationToken).ConfigureAwait(false);

    if (holder is { IsActive: false } && !string.Equals(holder.ExternalId, repository.ExternalId, StringComparison.Ordinal))
    {
      holder.ReleaseName(clock.UtcNow);
      await repositories.UpdateAsync(holder, cancellationToken).ConfigureAwait(false);
    }

    if (job is not null)
    {
      await repositories.SaveWithJobAsync(repository, job, cancellationToken).ConfigureAwait(false);
    }
    else if (isNew)
    {
      await repositories.AddAsync(repository, cancellationToken).ConfigureAwait(false);
    }
    else
    {
      await repositories.UpdateAsync(repository, cancellationToken).ConfigureAwait(false);
    }
  }

  /// <summary>Tells whether the account of an organization holds a repository, as its forge reports it.</summary>
  /// <param name="reported">What the forge reports.</param>
  /// <param name="organization">The organization.</param>
  /// <returns><see langword="true" /> when the organization holds the repository.</returns>
  /// <remarks>By the identifier of the account whenever the forge gives it: a login can be renamed, or taken again by another account.</remarks>
  private static bool IsOwnedBy(ForgeRepositoryInfo reported, Organization organization) =>
    reported.OwnerExternalId is { } ownerId
      ? string.Equals(ownerId, organization.ExternalAccountId, StringComparison.OrdinalIgnoreCase)
      : string.Equals(reported.Owner, organization.Login, StringComparison.OrdinalIgnoreCase);

  /// <summary>Builds the details of an audit entry that counts tokens.</summary>
  /// <param name="count">How many tokens the action touched.</param>
  /// <returns>The details.</returns>
  private static Dictionary<string, string> Count(int count) =>
    new(StringComparer.Ordinal) { ["Tokens"] = count.ToString(CultureInfo.InvariantCulture) };

  #endregion
}
