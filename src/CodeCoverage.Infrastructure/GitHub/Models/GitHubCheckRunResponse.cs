// ------------------------------------------------------------------------------------------------
// <copyright file="GitHubCheckRunResponse.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Infrastructure.GitHub.Models;

/// <summary>A check run, as its endpoints answer it.</summary>
/// <param name="Id">The identifier of the check run.</param>
internal sealed record GitHubCheckRunResponse(long Id);
