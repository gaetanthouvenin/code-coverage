// ------------------------------------------------------------------------------------------------
// <copyright file="GitHubJsonContext.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Text.Json.Serialization;

using CodeCoverage.Infrastructure.GitHub.Models;

namespace CodeCoverage.Infrastructure.GitHub;

/// <summary>The source-generated serialization of the payloads of the GitHub API, in <c>snake_case</c>.</summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower)]
[JsonSerializable(typeof(GitHubInstallationTokenResponse))]
internal sealed partial class GitHubJsonContext : JsonSerializerContext;
