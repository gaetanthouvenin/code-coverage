// ------------------------------------------------------------------------------------------------
// <copyright file="ForgeConnectionInfo.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Domain.Contracts.Forges;

/// <summary>What a forge reports about the connection that serves a repository found by its name.</summary>
/// <param name="ConnectionId">The connection that gives access to the repository: the installation identifier on GitHub.</param>
/// <param name="ExternalAccountId">The identifier the forge gives the account that owns the connection.</param>
/// <param name="RepositoryExternalId">The identifier the forge gives the repository.</param>
public sealed record ForgeConnectionInfo(string ConnectionId, string ExternalAccountId, string RepositoryExternalId);
