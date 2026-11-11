// ------------------------------------------------------------------------------------------------
// <copyright file="GitHubIssueCommentRequest.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Infrastructure.GitHub.Models;

/// <summary>The body of the requests that create or update a comment.</summary>
/// <param name="Body">The text of the comment.</param>
internal sealed record GitHubIssueCommentRequest(string Body);
