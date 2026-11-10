// ------------------------------------------------------------------------------------------------
// <copyright file="ReprocessCommitCommand.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using CodeCoverage.Application.Security;
using CodeCoverage.Domain.Git;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Users;

namespace CodeCoverage.Application.Features.Commits.Commands.ReprocessCommit;

/// <summary>Asks for the uploads of a commit to be parsed again from their raw files, then its report to be rebuilt.</summary>
/// <param name="Forge">The forge of the repository.</param>
/// <param name="Owner">The owner of the repository, as its address writes it.</param>
/// <param name="Name">The name of the repository.</param>
/// <param name="Sha">The SHA of the commit.</param>
/// <remarks>
///   Nothing is parsed in the request: the command checks that the raw files are still kept, then queues
///   the <c>ReprocessCommit</c> job, which holds the lock of the commit.
/// </remarks>
public sealed record ReprocessCommitCommand(Forge Forge, string Owner, string Name, GitSha Sha) : ICommand, IRepositoryScoped
{
  #region Properties

  /// <inheritdoc />
  public RepositoryPermission RequiredPermission => RepositoryPermission.Admin;

  #endregion
}
