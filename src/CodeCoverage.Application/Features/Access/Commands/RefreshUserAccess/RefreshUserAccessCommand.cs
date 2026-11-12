// ------------------------------------------------------------------------------------------------
// <copyright file="RefreshUserAccessCommand.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using CodeCoverage.Application.Security;

namespace CodeCoverage.Application.Features.Access.Commands.RefreshUserAccess;

/// <summary>
///   Asks the forge again which repositories of the organization of the job a user may read, and how: the work of the
///   <c>RefreshUserAccess</c> job.
/// </summary>
/// <param name="UserId">The identifier of the user.</param>
/// <remarks>Sent by the job only, inside the organization of its lane.</remarks>
public sealed record RefreshUserAccessCommand(long UserId) : ICommand, ISystemOnly;
