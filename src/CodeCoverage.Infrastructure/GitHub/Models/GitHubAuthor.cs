// ------------------------------------------------------------------------------------------------
// <copyright file="GitHubAuthor.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Infrastructure.GitHub.Models;

/// <summary>The account attached to a commit, to a pull request or to a comment.</summary>
/// <param name="Login">The login of the user; GitHub answers with an empty object when the author matches no account.</param>
internal sealed record GitHubAuthor(string? Login = null);
