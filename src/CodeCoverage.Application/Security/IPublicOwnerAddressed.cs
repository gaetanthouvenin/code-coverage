// ------------------------------------------------------------------------------------------------
// <copyright file="IPublicOwnerAddressed.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Organizations;

namespace CodeCoverage.Application.Security;

/// <summary>
///   Marks a message that anyone may send to an owner named in its address, such as an upload or a
///   badge, and that its handler authenticates on its own.
/// </summary>
/// <remarks>
///   <see cref="Behaviors.AuthorizationBehavior{TMessage,TResponse}" /> enters the organization when
///   it is active and otherwise leaves the scope unset without refusing: the handler then gives the
///   answer it gives for a wrong credential, so that the response does not reveal the organization.
/// </remarks>
public interface IPublicOwnerAddressed
{
  #region Properties

  /// <summary>Gets the forge of the addressed owner.</summary>
  Forge Forge { get; }

  /// <summary>
  ///   Gets the addressed owner, as received from the caller; what precedes a first <c>:</c> names the
  ///   organization.
  /// </summary>
  string Owner { get; }

  #endregion
}
