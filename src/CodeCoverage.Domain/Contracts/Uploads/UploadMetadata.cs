// ------------------------------------------------------------------------------------------------
// <copyright file="UploadMetadata.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Git;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Repositories;

namespace CodeCoverage.Domain.Contracts.Uploads;

/// <summary>What the text fields of an upload describe.</summary>
/// <param name="Slug">The repository.</param>
/// <param name="Sha">The commit; the head of a pull request, never its merge commit.</param>
/// <param name="Branch">The branch, or <see langword="null" />.</param>
/// <param name="PullNumber">The number of the pull request, a hint, or <see langword="null" />.</param>
/// <param name="Flags">The flags of the upload.</param>
/// <param name="Name">The name of the session, or <see langword="null" />.</param>
/// <param name="Root">The absolute root of the repository on the agent, or <see langword="null" />.</param>
/// <param name="UploaderVersion">The version of the uploader, or <see langword="null" />.</param>
/// <param name="Ci">Where the upload was sent from.</param>
/// <param name="Forge">The forge of the repository: GitHub for an uploader that does not name one (ADR-014).</param>
public sealed record UploadMetadata(
  RepositorySlug Slug,
  GitSha Sha,
  string? Branch,
  int? PullNumber,
  IReadOnlyList<string> Flags,
  string? Name,
  string? Root,
  string? UploaderVersion,
  CiContext Ci,
  Forge Forge = Forge.GitHub);
