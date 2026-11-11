// ------------------------------------------------------------------------------------------------
// <copyright file="GitHubMembershipUser.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Infrastructure.GitHub.Models;

/// <summary>The account a membership of an organization describes.</summary>
/// <param name="Id">The identifier of the account, or <see langword="null" /> when GitHub gives none.</param>
internal sealed record GitHubMembershipUser(long? Id = null);
