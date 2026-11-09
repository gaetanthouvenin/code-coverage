// ------------------------------------------------------------------------------------------------
// <copyright file="GitHubOidcIdentity.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Domain.Contracts.Identity;

/// <summary>What a verified GitHub Actions OIDC token proves about the workflow that requested it.</summary>
/// <param name="Repository">The <c>owner/name</c> of the repository, which changes when the repository is renamed.</param>
/// <param name="RepositoryId">
///   The identifier GitHub gives the repository (<c>repository_id</c>), to compare with its external identifier: it
///   survives a renaming.
/// </param>
/// <param name="RepositoryOwner">The login of the account that owns the repository.</param>
/// <param name="RepositoryOwnerId">
///   The identifier GitHub gives that account (<c>repository_owner_id</c>), to compare with the account of the
///   organization.
/// </param>
/// <param name="Ref">The Git reference that triggered the workflow, or <see langword="null" />.</param>
/// <param name="EventName">The event that triggered the workflow, or <see langword="null" />.</param>
/// <remarks>
///   The token proves the identity of the repository, not the commit. Nothing here tells whether the repository is
///   allowed to upload: the caller decides by comparing the two identifiers.
/// </remarks>
public sealed record GitHubOidcIdentity(
  string Repository,
  string RepositoryId,
  string RepositoryOwner,
  string RepositoryOwnerId,
  string? Ref,
  string? EventName);
