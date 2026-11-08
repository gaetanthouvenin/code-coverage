// ------------------------------------------------------------------------------------------------
// <copyright file="IUserScoped.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Application.Security;

/// <summary>Marks a message about the signed-in user itself, which enters no organization.</summary>
/// <remarks>
///   Checked by <see cref="Behaviors.AuthorizationBehavior{TMessage,TResponse}" />: an anonymous
///   sender is refused with an <see cref="Exceptions.UnauthorizedException" />.
/// </remarks>
public interface IUserScoped;
