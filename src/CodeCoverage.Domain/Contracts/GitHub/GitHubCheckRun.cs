// ------------------------------------------------------------------------------------------------
// <copyright file="GitHubCheckRun.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Git;
using CodeCoverage.Domain.Notifications;

namespace CodeCoverage.Domain.Contracts.GitHub;

/// <summary>A check run to create or update on a commit, in place of a commit status (M5.05).</summary>
/// <param name="Name">The name of the check run: the context the status would have, so that branch protections match.</param>
/// <param name="HeadSha">The commit.</param>
/// <param name="State">The state of the status it replaces, which gives its conclusion.</param>
/// <param name="Summary">The description of the status, its title and summary.</param>
/// <param name="DetailsUrl">The address it links to, or <see langword="null" />.</param>
/// <param name="Annotations">The annotations of this call, <see cref="CheckRunAnnotationBuilder.BatchSize" /> at most.</param>
public sealed record GitHubCheckRun(
  string Name,
  GitSha HeadSha,
  StatusState State,
  string Summary,
  string? DetailsUrl,
  IReadOnlyList<CheckRunAnnotation> Annotations);
