// ------------------------------------------------------------------------------------------------
// <copyright file="IRepositoryScoped.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Users;

namespace CodeCoverage.Application.Security;

/// <summary>
///   Marks a message that targets one tracked repository and requires a permission on it.
/// </summary>
/// <remarks>
///   <see cref="Behaviors.AuthorizationBehavior{TMessage,TResponse}" /> enters the organization of the
///   owner before the validation. An unknown, malformed or non-active owner is refused with a
///   <see cref="Exceptions.NotFoundException" />, like a repository the sender cannot read. Until the
///   permission is checked with the repository access service, every sender other than the system is
///   refused the same way.
/// </remarks>
public interface IRepositoryScoped
{
  #region Properties

  /// <summary>Gets the forge of the targeted repository.</summary>
  Forge Forge { get; }

  /// <summary>
  ///   Gets the owner of the targeted repository, as received from the caller; what precedes a first
  ///   <c>:</c> names the organization.
  /// </summary>
  string Owner { get; }

  /// <summary>Gets the name of the targeted repository on its forge.</summary>
  string Name { get; }

  /// <summary>Gets the permission the sender must hold on the targeted repository.</summary>
  RepositoryPermission RequiredPermission { get; }

  #endregion
}
