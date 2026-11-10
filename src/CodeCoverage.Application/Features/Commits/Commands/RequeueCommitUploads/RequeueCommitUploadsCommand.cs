// ------------------------------------------------------------------------------------------------
// <copyright file="RequeueCommitUploadsCommand.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using CodeCoverage.Application.Security;

namespace CodeCoverage.Application.Features.Commits.Commands.RequeueCommitUploads;

/// <summary>Puts the ended uploads of a commit back in the queue: the work of the <c>ReprocessCommit</c> job.</summary>
/// <param name="CommitId">The identifier of the commit.</param>
/// <remarks>
///   Sent by the job only, which holds the lock <c>commit:{id}</c>: no finalization of the commit runs
///   meanwhile. Each upload is then parsed again by its own job, and the last one to end queues the
///   finalization that rebuilds the report.
/// </remarks>
public sealed record RequeueCommitUploadsCommand(long CommitId) : ICommand, ISystemOnly;
