// ------------------------------------------------------------------------------------------------
// <copyright file="GitHubCheckRunOutput.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Infrastructure.GitHub.Models;

/// <summary>The output of a check run.</summary>
/// <param name="Title">The title.</param>
/// <param name="Summary">The summary, 65 535 characters at most.</param>
/// <param name="Annotations">The annotations of this call, 50 at most; GitHub adds them to those already sent.</param>
internal sealed record GitHubCheckRunOutput(string Title, string Summary, IReadOnlyList<GitHubCheckRunAnnotation> Annotations);
