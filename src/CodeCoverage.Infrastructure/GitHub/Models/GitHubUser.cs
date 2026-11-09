// ------------------------------------------------------------------------------------------------
// <copyright file="GitHubUser.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Infrastructure.GitHub.Models;

/// <summary>The user attached to a commit or to a pull request.</summary>
/// <param name="Login">The login of the user; GitHub answers with an empty object when the author matches no account.</param>
internal sealed record GitHubUser(string? Login = null);
