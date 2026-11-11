// ------------------------------------------------------------------------------------------------
// <copyright file="GitHubOrganizationMembership.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Domain.Contracts.GitHub;

/// <summary>What GitHub says of a user in an organization.</summary>
/// <param name="Active">Whether the membership is active; an invitation that was not accepted yet is not.</param>
/// <param name="Admin">Whether the user owns the organization (the <c>admin</c> role of GitHub).</param>
/// <param name="UserExternalId">
///   The identifier of the account GitHub describes, or <see langword="null" /> when the answer names none. The
///   membership is read by login, and a login can change hands: only this identifier tells whose membership it is.
/// </param>
public sealed record GitHubOrganizationMembership(bool Active, bool Admin, string? UserExternalId = null);
