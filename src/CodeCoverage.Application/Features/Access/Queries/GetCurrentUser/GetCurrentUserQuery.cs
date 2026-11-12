// ------------------------------------------------------------------------------------------------
// <copyright file="GetCurrentUserQuery.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using CodeCoverage.Application.Security;
using CodeCoverage.Domain.Users;

namespace CodeCoverage.Application.Features.Access.Queries.GetCurrentUser;

/// <summary>Reads the signed-in user.</summary>
/// <remarks>
///   It reads the global table of the users and enters no organization. The user carries its protected tokens: a host
///   projects it to what it shows before anything else touches it.
/// </remarks>
public sealed record GetCurrentUserQuery : IQuery<AppUser>, IUserScoped;
