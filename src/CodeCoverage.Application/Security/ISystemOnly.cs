// ------------------------------------------------------------------------------------------------
// <copyright file="ISystemOnly.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Application.Security;

/// <summary>
///   Marks a message that only the system principal (jobs, webhook processing) may send.
/// </summary>
/// <remarks>
///   Checked by <see cref="Behaviors.AuthorizationBehavior{TMessage,TResponse}" />: any other
///   sender, signed in or not, is refused with a <see cref="Exceptions.ForbiddenException" />,
///   since no sign-in can ever grant it. The HTTP hosts never produce the system principal.
/// </remarks>
public interface ISystemOnly;
