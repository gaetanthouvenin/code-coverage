// ------------------------------------------------------------------------------------------------
// <copyright file="CarryforwardOrigin.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Commits;

namespace CodeCoverage.Application.Services;

/// <summary>An ancestor that gives some missing flags.</summary>
/// <param name="Ancestor">The ancestor.</param>
/// <param name="Sessions">Its sessions.</param>
/// <param name="Flags">The missing flags it gives.</param>
internal sealed record CarryforwardOrigin(Commit Ancestor, IReadOnlyList<Session> Sessions, IReadOnlyList<string> Flags);
