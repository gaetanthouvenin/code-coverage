// ------------------------------------------------------------------------------------------------
// <copyright file="SyncPullRequestCommand.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using CodeCoverage.Application.Security;

namespace CodeCoverage.Application.Features.PullRequests.Commands.SyncPullRequest;

/// <summary>Reads a pull request again from its forge and records it: the work of the <c>SyncPullRequest</c> job.</summary>
/// <param name="RepositoryId">The identifier of the repository.</param>
/// <param name="Number">The number of the pull request.</param>
/// <remarks>Sent by the job only, which holds the lock <c>pull:{repositoryId}:{number}</c>.</remarks>
public sealed record SyncPullRequestCommand(long RepositoryId, int Number) : ICommand, ISystemOnly;
