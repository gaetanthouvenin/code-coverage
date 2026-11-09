// ------------------------------------------------------------------------------------------------
// <copyright file="GitHubAccount.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Infrastructure.GitHub.Models;

/// <summary>The account an installation belongs to, a user or an organization; only its identifier is common to all.</summary>
/// <param name="Id">The identifier of the account.</param>
internal sealed record GitHubAccount(long Id);
