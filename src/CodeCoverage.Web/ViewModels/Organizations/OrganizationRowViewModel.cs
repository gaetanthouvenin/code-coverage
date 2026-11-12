// ------------------------------------------------------------------------------------------------
// <copyright file="OrganizationRowViewModel.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Web.ViewModels.Organizations;

/// <summary>An organization of the instance, as the page of its administrators lists it.</summary>
/// <param name="Forge">The forge segment of its address, e.g. <c>github</c>.</param>
/// <param name="ForgeName">The name of its forge in the resources (<c>Forge_{name}</c>).</param>
/// <param name="Login">Its login.</param>
/// <param name="ExternalAccountId">The identifier of its account on its forge.</param>
/// <param name="IsUser">Whether its account is a personal account rather than an organization.</param>
/// <param name="State">Where it stands.</param>
/// <param name="SuspensionReason">The name of the reason of its suspension in the resources (<c>SuspensionReason_{name}</c>), or <see langword="null" />.</param>
/// <param name="AdmittedBy">Who admitted it, or <see langword="null" />.</param>
/// <param name="QueueDepth">Its jobs queued or running, or <see langword="null" /> when there is none.</param>
public sealed record OrganizationRowViewModel(
  string Forge,
  string ForgeName,
  string Login,
  string ExternalAccountId,
  bool IsUser,
  OrganizationLifecycle State,
  string? SuspensionReason,
  string? AdmittedBy,
  int? QueueDepth);
