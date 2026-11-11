// ------------------------------------------------------------------------------------------------
// <copyright file="GitHubUserRepository.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Users;

namespace CodeCoverage.Domain.Contracts.GitHub;

/// <summary>A repository of an installation that a user can reach on GitHub, with what the user may do there.</summary>
/// <param name="ExternalId">The numeric identifier GitHub gives the repository, as text.</param>
/// <param name="Permission">The highest permission of the user on the repository.</param>
/// <remarks>What GitHub reports, not a row of this application: the access of a user is kept in <see cref="RepositoryAccess" />.</remarks>
public sealed record GitHubUserRepository(string ExternalId, RepositoryPermission Permission);
