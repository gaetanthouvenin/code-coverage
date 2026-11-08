// ------------------------------------------------------------------------------------------------
// <copyright file="IOrganizationScoped.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Organizations;

namespace CodeCoverage.Application.Security;

/// <summary>Marks a message that targets one organization and requires a role in it.</summary>
/// <remarks>
///   <see cref="Behaviors.AuthorizationBehavior{TMessage,TResponse}" /> enters the organization before
///   the validation. An unknown, malformed or non-active organization is refused with a
///   <see cref="Exceptions.NotFoundException" />, like one the sender cannot see. Until the role is
///   checked, every sender other than the system is refused the same way.
/// </remarks>
public interface IOrganizationScoped
{
  #region Properties

  /// <summary>Gets the forge of the targeted organization.</summary>
  Forge Forge { get; }

  /// <summary>Gets the login of the targeted organization, as received from the caller.</summary>
  string Login { get; }

  /// <summary>Gets the role the sender must hold in the targeted organization.</summary>
  OrganizationRole RequiredRole { get; }

  #endregion
}
