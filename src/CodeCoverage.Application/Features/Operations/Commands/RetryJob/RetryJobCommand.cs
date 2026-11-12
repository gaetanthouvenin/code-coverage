// ------------------------------------------------------------------------------------------------
// <copyright file="RetryJobCommand.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using CodeCoverage.Application.Security;

namespace CodeCoverage.Application.Features.Operations.Commands.RetryJob;

/// <summary>Puts a failed or dead job back in the queue, to run at once.</summary>
/// <param name="Id">The identifier of the job.</param>
/// <remarks>
///   The row itself is reset: its attempts go back to zero and it keeps its organization, which the worker enters from
///   the row when it runs it (ADR-013); the message itself enters none.
/// </remarks>
public sealed record RetryJobCommand(long Id) : ICommand, IAdminOnly;
