// ------------------------------------------------------------------------------------------------
// <copyright file="GitHubJsonContext.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Text.Json.Serialization;

using CodeCoverage.Infrastructure.GitHub.Models;

namespace CodeCoverage.Infrastructure.GitHub;

/// <summary>The source-generated serialization of the payloads of the GitHub API, in <c>snake_case</c>.</summary>
/// <remarks>
///   A missing or <see langword="null" /> field that a model declares as required makes the reading fail, so an
///   unexpected payload surfaces as an unreadable answer and never as a half-filled record.
/// </remarks>
[JsonSourceGenerationOptions(
  PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower,
  RespectNullableAnnotations = true,
  RespectRequiredConstructorParameters = true)]
[JsonSerializable(typeof(GitHubCommitResponse))]
[JsonSerializable(typeof(GitHubCompareResponse))]
[JsonSerializable(typeof(GitHubInstallationResponse))]
[JsonSerializable(typeof(GitHubInstallationRepositoriesResponse))]
[JsonSerializable(typeof(GitHubInstallationTokenResponse))]
[JsonSerializable(typeof(GitHubIssueCommentRequest))]
[JsonSerializable(typeof(GitHubIssueCommentResponse))]
[JsonSerializable(typeof(GitHubBranchResponse))]
[JsonSerializable(typeof(GitHubPullResponse))]
[JsonSerializable(typeof(GitHubRepositoryResponse))]
[JsonSerializable(typeof(GitHubStatusRequest))]
[JsonSerializable(typeof(GitHubTreeResponse))]
[JsonSerializable(typeof(GitHubManifestConversionResponse))]
[JsonSerializable(typeof(IReadOnlyList<GitHubCommitResponse>))]
[JsonSerializable(typeof(IReadOnlyList<GitHubCompareFile>))]
[JsonSerializable(typeof(GitHubInstallationListItem))]
[JsonSerializable(typeof(IReadOnlyList<GitHubInstallationListItem>))]
[JsonSerializable(typeof(IReadOnlyList<GitHubIssueCommentResponse>))]
internal sealed partial class GitHubJsonContext : JsonSerializerContext;
