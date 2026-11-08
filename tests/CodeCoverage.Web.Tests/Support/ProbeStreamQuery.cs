// ------------------------------------------------------------------------------------------------
// <copyright file="ProbeStreamQuery.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

namespace CodeCoverage.Web.Tests.Support;

// Answered by the fake sender of each scope with a stream that yields the ScopeProbe of that scope.
public sealed record ProbeStreamQuery : IStreamQuery<ScopeProbe>;
