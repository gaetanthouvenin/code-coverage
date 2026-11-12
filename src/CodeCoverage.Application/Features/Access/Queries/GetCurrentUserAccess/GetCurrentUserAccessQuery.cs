// ------------------------------------------------------------------------------------------------
// <copyright file="GetCurrentUserAccessQuery.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using CodeCoverage.Application.Security;
using CodeCoverage.Domain.Users;

namespace CodeCoverage.Application.Features.Access.Queries.GetCurrentUserAccess;

/// <summary>Reads the signed-in user and whether it administers the instance.</summary>
/// <remarks>For the navigation only: each page and each message keeps checking the access itself.</remarks>
public sealed record GetCurrentUserAccessQuery : IQuery<UserAccess>, IUserScoped;
