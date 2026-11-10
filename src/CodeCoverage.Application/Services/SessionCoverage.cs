// ------------------------------------------------------------------------------------------------
// <copyright file="SessionCoverage.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Commits;
using CodeCoverage.Domain.Coverage;

namespace CodeCoverage.Application.Services;

/// <summary>A session of a commit and its coverage.</summary>
/// <param name="Session">The session.</param>
/// <param name="Report">Its coverage.</param>
internal sealed record SessionCoverage(Session Session, CoverageReport Report);
