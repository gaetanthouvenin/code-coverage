// ------------------------------------------------------------------------------------------------
// <copyright file="UploadAuthorization.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Commits;
using CodeCoverage.Domain.Contracts.Forges;

namespace CodeCoverage.Domain.Uploads;

/// <summary>What a credential proved for an upload to a repository.</summary>
/// <param name="PrincipalKind">The kind of credential that sent the upload.</param>
/// <param name="TokenId">The identifier of the upload token, or <see langword="null" /> for an OIDC token.</param>
/// <param name="RepositoryId">The identifier of the tracked repository, or <see langword="null" /> when the instance does not track it yet.</param>
/// <param name="Connection">What the forge reported about an unknown repository an OIDC token named, or <see langword="null" />.</param>
public sealed record UploadAuthorization(
  PrincipalKind PrincipalKind,
  long? TokenId,
  long? RepositoryId,
  ForgeConnectionInfo? Connection);
