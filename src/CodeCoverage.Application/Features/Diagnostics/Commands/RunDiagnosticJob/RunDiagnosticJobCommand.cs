// ------------------------------------------------------------------------------------------------
// <copyright file="RunDiagnosticJobCommand.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using CodeCoverage.Application.Security;

namespace CodeCoverage.Application.Features.Diagnostics.Commands.RunDiagnosticJob;

/// <summary>
///   Runs the diagnostic job (<c>Ping</c>): does nothing but go through the whole pipeline of the job
///   worker, to check end to end that a host runs its jobs as the system principal.
/// </summary>
/// <param name="JobId">The identifier of the job that sends the command.</param>
/// <remarks>Sent by the job worker only (<see cref="ISystemOnly" />): no user can trigger it.</remarks>
public sealed record RunDiagnosticJobCommand(long JobId) : ICommand, ISystemOnly;
