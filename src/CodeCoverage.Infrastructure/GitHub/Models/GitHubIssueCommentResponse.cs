// ------------------------------------------------------------------------------------------------
// <copyright file="GitHubIssueCommentResponse.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Infrastructure.GitHub.Models;

/// <summary>A comment of an issue or of a pull request, as the issue comments endpoints answer it.</summary>
/// <param name="Id">The identifier of the comment.</param>
/// <param name="Body">The text of the comment, or <see langword="null" /> for an empty one.</param>
/// <param name="User">Who wrote the comment; an application writes as <c>{slug}[bot]</c>.</param>
internal sealed record GitHubIssueCommentResponse(long Id, string? Body = null, GitHubUser? User = null);
