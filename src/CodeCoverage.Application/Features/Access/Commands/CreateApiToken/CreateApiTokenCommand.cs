// ------------------------------------------------------------------------------------------------
// <copyright file="CreateApiTokenCommand.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using CodeCoverage.Application.Security;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Tokens;

namespace CodeCoverage.Application.Features.Access.Commands.CreateApiToken;

/// <summary>Creates a personal token of the API for the signed-in user, bound to one organization or to the instance.</summary>
/// <param name="Name">The name given to the token.</param>
/// <param name="Forge">The forge of the organization of the token, or <see langword="null" /> for a token of the instance.</param>
/// <param name="Organization">The login of the organization of the token, or <see langword="null" /> for a token of the instance.</param>
/// <param name="ExpiresAt">When it expires, or <see langword="null" />.</param>
/// <remarks>
///   <para>
///     A token of an organization is created by any member of that active organization; a token of the instance only by
///     an administrator of the instance. The token grants nothing beyond the rights of its user.
///   </para>
///   <para>
///     A request already authenticated by a token stays within it: a token of an organization creates tokens of that
///     organization only, a token of the instance tokens of the instance only.
///   </para>
/// </remarks>
public sealed record CreateApiTokenCommand(string Name, Forge? Forge, string? Organization, DateTimeOffset? ExpiresAt) : ICommand<IssuedApiToken>, IUserScoped;
