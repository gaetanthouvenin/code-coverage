// ------------------------------------------------------------------------------------------------
// <copyright file="FinalizeCommitCommand.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using CodeCoverage.Application.Security;

namespace CodeCoverage.Application.Features.Commits.Commands.FinalizeCommit;

/// <summary>Rebuilds the report of a commit from its uploads, in a new revision.</summary>
/// <param name="CommitId">The identifier of the commit.</param>
public sealed record FinalizeCommitCommand(long CommitId) : ICommand, ISystemOnly;
