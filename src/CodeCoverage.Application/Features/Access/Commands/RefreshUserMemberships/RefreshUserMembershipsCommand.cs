// ------------------------------------------------------------------------------------------------
// <copyright file="RefreshUserMembershipsCommand.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using CodeCoverage.Application.Security;

namespace CodeCoverage.Application.Features.Access.Commands.RefreshUserMemberships;

/// <summary>
///   Asks the forge again which organizations a user is linked to, and with which role: the work of the
///   <c>RefreshUserMemberships</c> job.
/// </summary>
/// <param name="UserId">The identifier of the user.</param>
/// <remarks>Sent by the job only, on the lane of the instance: the memberships of a user span several organizations.</remarks>
public sealed record RefreshUserMembershipsCommand(long UserId) : ICommand, ISystemOnly;
