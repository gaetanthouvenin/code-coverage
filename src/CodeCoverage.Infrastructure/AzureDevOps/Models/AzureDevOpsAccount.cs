// ------------------------------------------------------------------------------------------------
// <copyright file="AzureDevOpsAccount.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Infrastructure.AzureDevOps.Models;

/// <summary>An Azure DevOps organization a user is a member of, as <c>_apis/accounts?memberId</c> reports it.</summary>
/// <param name="AccountId">The identifier of the organization.</param>
/// <param name="AccountName">The name of the organization, the first segment of its address.</param>
/// <remarks>
///   The state of the organization is left out: the reference does not say how it is serialized, and a field read
///   wrongly would make the whole listing unreadable.
/// </remarks>
internal sealed record AzureDevOpsAccount(Guid AccountId, string AccountName);
