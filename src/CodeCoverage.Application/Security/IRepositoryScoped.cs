// ------------------------------------------------------------------------------------------------
// <copyright file="IRepositoryScoped.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Users;

namespace CodeCoverage.Application.Security;

/// <summary>
///   Marks a message that targets one tracked repository and requires a permission on it.
/// </summary>
/// <remarks>
///   Declared for the use cases to come; the check (a missing read permission hides the repository
///   behind a <see cref="Exceptions.NotFoundException" />, a missing administration permission
///   raises a <see cref="Exceptions.ForbiddenException" />) is added to
///   <see cref="Behaviors.AuthorizationBehavior{TMessage,TResponse}" /> with the repository
///   access service.
/// </remarks>
public interface IRepositoryScoped
{
  #region Properties

  /// <summary>Gets the GitHub owner (user or organization) of the targeted repository.</summary>
  string Owner { get; }

  /// <summary>Gets the GitHub name of the targeted repository.</summary>
  string Name { get; }

  /// <summary>Gets the permission the sender must hold on the targeted repository.</summary>
  RepositoryPermission RequiredPermission { get; }

  #endregion
}
