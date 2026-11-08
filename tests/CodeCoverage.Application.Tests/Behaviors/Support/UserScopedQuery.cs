// ------------------------------------------------------------------------------------------------
// <copyright file="UserScopedQuery.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using CodeCoverage.Application.Security;

namespace CodeCoverage.Application.Tests.Behaviors.Support;

// A query about the signed-in user.
internal sealed record UserScopedQuery : IQuery<string>, IUserScoped;
