// ------------------------------------------------------------------------------------------------
// <copyright file="ForgeMembership.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Domain.Contracts.Forges;

/// <summary>What a forge says of a user in the account of an organization.</summary>
/// <param name="Active">Whether the membership is active; an invitation that was not accepted yet is not.</param>
/// <param name="Admin">Whether the forge lets the user administer the account (the <c>admin</c> role on GitHub).</param>
/// <param name="UserExternalId">
///   The identifier of the user the forge describes, or <see langword="null" /> when the answer names none. A membership
///   can be read by login, and a login can change hands: only this identifier tells whose membership it is.
/// </param>
public sealed record ForgeMembership(bool Active, bool Admin, string? UserExternalId = null);
