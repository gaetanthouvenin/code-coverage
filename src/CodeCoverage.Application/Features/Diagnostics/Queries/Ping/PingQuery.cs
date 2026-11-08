// ------------------------------------------------------------------------------------------------
// <copyright file="PingQuery.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using CodeCoverage.Application.Security;

namespace CodeCoverage.Application.Features.Diagnostics.Queries.Ping;

/// <summary>
///   Gets the version of the Application, to check that a host is wired to the mediator
///   (diagnostic use case, kept for the smoke tests).
/// </summary>
public sealed record PingQuery : IQuery<string>, IAnonymousEntry;
