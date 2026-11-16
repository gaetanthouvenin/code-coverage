// ------------------------------------------------------------------------------------------------
// <copyright file="ForgeRepositoryPermission.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Users;

namespace CodeCoverage.Domain.Contracts.Forges;

/// <summary>A repository that a user reaches on a forge, with what the user may do there.</summary>
/// <param name="RepositoryExternalId">The identifier the forge gives the repository, as text.</param>
/// <param name="Permission">The highest permission of the user on the repository.</param>
/// <remarks>What the forge reports, not a row of this application: the access of a user is kept in <see cref="RepositoryAccess" />.</remarks>
public sealed record ForgeRepositoryPermission(string RepositoryExternalId, RepositoryPermission Permission);
