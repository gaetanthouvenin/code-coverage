// ------------------------------------------------------------------------------------------------
// <copyright file="CheckInstallationsCommand.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using CodeCoverage.Application.Security;

namespace CodeCoverage.Application.Features.Organizations.Commands.CheckInstallations;

/// <summary>Queues the listing of the installations of the GitHub App now, without waiting for the nightly one.</summary>
/// <remarks>
///   It catches up an installation whose webhook was lost: the coordinator creates its pending organization, or lists
///   the repositories of an active one. Reserved to the administrators of the instance, and audited.
/// </remarks>
public sealed record CheckInstallationsCommand : ICommand, IAdminOnly;
