// ------------------------------------------------------------------------------------------------
// <copyright file="RevokeApiTokenCommand.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using CodeCoverage.Application.Security;

namespace CodeCoverage.Application.Features.Access.Commands.RevokeApiToken;

/// <summary>Revokes a personal token of the API of the signed-in user.</summary>
/// <param name="Id">The identifier of the token.</param>
/// <remarks>A token of another user, or out of reach of the token that authenticates the request, is unknown.</remarks>
public sealed record RevokeApiTokenCommand(long Id) : ICommand, IUserScoped;
