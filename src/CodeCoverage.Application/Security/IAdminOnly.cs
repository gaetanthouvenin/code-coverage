// ------------------------------------------------------------------------------------------------
// <copyright file="IAdminOnly.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Application.Security;

/// <summary>
///   Marks a message that only an administrator of the instance, or the system, may send.
/// </summary>
/// <remarks>
///   Checked by <see cref="Behaviors.AuthorizationBehavior{TMessage,TResponse}" />: an anonymous
///   sender is refused with an <see cref="Exceptions.UnauthorizedException" />, a signed-in user
///   who is not an administrator with a <see cref="Exceptions.ForbiddenException" />.
/// </remarks>
public interface IAdminOnly;
