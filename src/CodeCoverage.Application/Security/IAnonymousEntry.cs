// ------------------------------------------------------------------------------------------------
// <copyright file="IAnonymousEntry.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Application.Security;

/// <summary>Marks a message that anyone may send, and that enters no organization.</summary>
/// <remarks>
///   <see cref="Behaviors.AuthorizationBehavior{TMessage,TResponse}" /> lets it through: either it
///   exposes nothing that needs protection, or its handler authenticates it, as a webhook through
///   its signature.
/// </remarks>
public interface IAnonymousEntry;
