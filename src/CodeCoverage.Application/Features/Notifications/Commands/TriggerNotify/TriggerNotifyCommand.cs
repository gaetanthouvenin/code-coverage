// ------------------------------------------------------------------------------------------------
// <copyright file="TriggerNotifyCommand.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using CodeCoverage.Application.Security;
using CodeCoverage.Domain.Git;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Users;

namespace CodeCoverage.Application.Features.Notifications.Commands.TriggerNotify;

/// <summary>Asks for a commit to be notified again on its forge, by hand.</summary>
/// <param name="Forge">The forge of the repository.</param>
/// <param name="Owner">The owner of the repository, as its address spells it.</param>
/// <param name="Name">The name of the repository.</param>
/// <param name="Sha">The SHA of the commit.</param>
/// <remarks>
///   The notification is forced, as the one of <c>uploads-complete</c>: it ignores <c>after_n_builds</c> and
///   <c>manual_trigger</c>, and waits for the uploads in progress. Reserved to the administrators of the
///   repository.
/// </remarks>
public sealed record TriggerNotifyCommand(Forge Forge, string Owner, string Name, GitSha Sha) : ICommand, IRepositoryScoped
{
  #region Properties

  /// <inheritdoc />
  public RepositoryPermission RequiredPermission => RepositoryPermission.Admin;

  #endregion
}
