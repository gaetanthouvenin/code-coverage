// ------------------------------------------------------------------------------------------------
// <copyright file="AzureDevOpsIdentity.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Infrastructure.AzureDevOps.Models;

/// <summary>An identity of Azure DevOps, such as the author of a pull request.</summary>
/// <param name="Id">The identifier of the identity, or <see langword="null" />.</param>
/// <param name="UniqueName">
///   The sign-in name of the identity, the user principal name of an Entra account, or <see langword="null" />.
/// </param>
/// <param name="DisplayName">The display name of the identity, or <see langword="null" />.</param>
internal sealed record AzureDevOpsIdentity(string? Id = null, string? UniqueName = null, string? DisplayName = null);
