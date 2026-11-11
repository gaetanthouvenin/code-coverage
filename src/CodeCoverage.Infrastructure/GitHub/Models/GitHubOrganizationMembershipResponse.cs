// ------------------------------------------------------------------------------------------------
// <copyright file="GitHubOrganizationMembershipResponse.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Infrastructure.GitHub.Models;

/// <summary>The membership of a user in an organization, as <c>GET /orgs/{org}/memberships/{username}</c> answers it.</summary>
/// <param name="State">The state of the membership: <c>active</c>, or <c>pending</c> for an invitation.</param>
/// <param name="Role">The role of the user: <c>admin</c>, <c>member</c> or <c>billing_manager</c>.</param>
/// <param name="User">The account the membership describes, or <see langword="null" />.</param>
internal sealed record GitHubOrganizationMembershipResponse(string? State = null, string? Role = null, GitHubMembershipUser? User = null);
