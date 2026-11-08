// ------------------------------------------------------------------------------------------------
// <copyright file="ForgePullInfo.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Git;

namespace CodeCoverage.Domain.Contracts.Forges;

/// <summary>What a forge reports about a pull request.</summary>
/// <param name="Number">The number of the pull request in its repository.</param>
/// <param name="State">The state of the pull request.</param>
/// <param name="Title">The title of the pull request.</param>
/// <param name="AuthorLogin">
///   The login of its author, or <see langword="null" /> when the author no longer has an account on the forge.
/// </param>
/// <param name="HeadSha">The SHA of its head commit, never the synthetic merge commit.</param>
/// <param name="HeadBranch">The branch of its head.</param>
/// <param name="BaseBranch">The branch it targets.</param>
/// <param name="BaseSha">The SHA of the base that the forge reports.</param>
public sealed record ForgePullInfo(
  int Number,
  ForgePullState State,
  string Title,
  string? AuthorLogin,
  GitSha HeadSha,
  string HeadBranch,
  string BaseBranch,
  GitSha BaseSha);
