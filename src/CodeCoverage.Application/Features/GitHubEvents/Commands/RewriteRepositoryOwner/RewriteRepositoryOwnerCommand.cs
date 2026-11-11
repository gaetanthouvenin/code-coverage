// ------------------------------------------------------------------------------------------------
// <copyright file="RewriteRepositoryOwnerCommand.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using CodeCoverage.Application.Security;

namespace CodeCoverage.Application.Features.GitHubEvents.Commands.RewriteRepositoryOwner;

/// <summary>
///   Rewrites the owner of the repositories of the organization of the scope with the login its account has now:
///   the work of the <c>RewriteRepositoryOwner</c> job, queued when the account is renamed.
/// </summary>
public sealed record RewriteRepositoryOwnerCommand : ICommand, ISystemOnly;
