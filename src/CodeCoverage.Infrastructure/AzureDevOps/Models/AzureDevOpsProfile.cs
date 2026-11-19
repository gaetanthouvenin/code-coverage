// ------------------------------------------------------------------------------------------------
// <copyright file="AzureDevOpsProfile.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Infrastructure.AzureDevOps.Models;

/// <summary>The profile of a user, as <c>_apis/profile/profiles/me</c> reports it.</summary>
/// <param name="Id">The identifier of the profile, which never changes: the identity of the user on Azure DevOps (ADR-014).</param>
/// <param name="DisplayName">The name Azure DevOps shows for the user.</param>
/// <remarks>The address and the alias of the user are left out: nothing here needs them.</remarks>
internal sealed record AzureDevOpsProfile(Guid Id, string DisplayName);
