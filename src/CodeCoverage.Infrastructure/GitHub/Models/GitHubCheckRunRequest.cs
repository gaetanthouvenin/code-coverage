// ------------------------------------------------------------------------------------------------
// <copyright file="GitHubCheckRunRequest.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Text.Json.Serialization;

namespace CodeCoverage.Infrastructure.GitHub.Models;

/// <summary>The body of <c>POST /repositories/{id}/check-runs</c> and of <c>PATCH /repositories/{id}/check-runs/{id}</c>.</summary>
/// <param name="Name">The name of the check run, on a creation only.</param>
/// <param name="HeadSha">The commit, on a creation only.</param>
/// <param name="Status">Always <c>completed</c>: a check run is published once its status is known.</param>
/// <param name="Conclusion"><c>success</c> or <c>failure</c>.</param>
/// <param name="DetailsUrl">The address it links to, or <see langword="null" />.</param>
/// <param name="Output">The title, the summary and the annotations.</param>
internal sealed record GitHubCheckRunRequest(
  [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Name,
  [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? HeadSha,
  string Status,
  string Conclusion,
  [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? DetailsUrl,
  GitHubCheckRunOutput Output);
