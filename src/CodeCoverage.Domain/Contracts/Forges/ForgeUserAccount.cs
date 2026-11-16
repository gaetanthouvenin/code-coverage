// ------------------------------------------------------------------------------------------------
// <copyright file="ForgeUserAccount.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Organizations;

namespace CodeCoverage.Domain.Contracts.Forges;

/// <summary>An account of a forge that a user reaches through the access the forge gives this instance.</summary>
/// <param name="ConnectionId">
///   The access through which the account is reached, as text: the installation identifier on GitHub. The repositories
///   of the account are listed through it.
/// </param>
/// <param name="AccountId">The identifier the forge gives the account, as text.</param>
/// <param name="AccountLogin">The login of the account, empty for an account that has none, such as an enterprise.</param>
/// <param name="AccountType">The kind of the account.</param>
/// <param name="Suspended">Whether the access is suspended: the forge then refuses every call made through it.</param>
/// <remarks>
///   What the forge reports, whatever the state of the organization of the account in this instance: an organization, a
///   workspace, a group or a personal namespace, depending on the forge.
/// </remarks>
public sealed record ForgeUserAccount(string ConnectionId, string AccountId, string AccountLogin, OrganizationAccountType AccountType, bool Suspended);
