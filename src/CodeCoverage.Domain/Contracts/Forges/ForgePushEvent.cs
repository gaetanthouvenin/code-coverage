// ------------------------------------------------------------------------------------------------
// <copyright file="ForgePushEvent.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Git;

namespace CodeCoverage.Domain.Contracts.Forges;

/// <summary>A push to a branch of a repository, as any forge reports it once its delivery is translated.</summary>
/// <param name="RepositoryExternalId">The identifier the forge gives the repository, which survives a renaming.</param>
/// <param name="Branch">The name of the branch; a push that is not about a branch, a tag for instance, is never translated.</param>
/// <param name="Head">The commit the branch points to after the push, or <see langword="null" /> when the branch was deleted.</param>
/// <param name="Deleted">Whether the push deleted the branch.</param>
/// <param name="TouchesConfig">
///   Whether the push may have changed the configuration file: a commit of the push adds or modifies one, or the push
///   was forced, which may take the branch back to a commit whose file differs.
/// </param>
/// <param name="Before">
///   The commit the branch pointed to before the push, or <see langword="null" /> when the push created the branch or
///   the forge does not say.
/// </param>
/// <remarks>
///   Nothing here is read as a history: the forge is asked where the branch is when the push does not start where the
///   branch is recorded, and the content of a configuration file always comes from the forge.
/// </remarks>
public sealed record ForgePushEvent(
  string RepositoryExternalId,
  string Branch,
  GitSha? Head,
  bool Deleted,
  bool TouchesConfig,
  GitSha? Before = null) : ForgeEvent;
