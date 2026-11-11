// ------------------------------------------------------------------------------------------------
// <copyright file="CommentContent.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Commits;
using CodeCoverage.Domain.Config;
using CodeCoverage.Domain.PullRequests;

namespace CodeCoverage.Application.Services.Comments;

/// <summary>Everything the comment of a pull request is written from.</summary>
/// <param name="Marker">The hidden first line by which the comment is found again on the forge.</param>
/// <param name="Title">The title of the comment (<c>Notifications:CommentTitle</c>).</param>
/// <param name="Config">The configuration of the commit: the rule of the comment, the precision and the rounding.</param>
/// <param name="Head">The commit that is commented: the head of the pull request.</param>
/// <param name="Pull">The pull request.</param>
/// <param name="Context">The comparison of the head with its base, and how that base was found.</param>
/// <param name="HeadFileCount">The number of files of the report of the head.</param>
/// <param name="BaseFileCount">The number of files of the report of the base, or <see langword="null" /> without one.</param>
/// <param name="FailedUploadCount">The number of uploads of the head that failed.</param>
/// <param name="Links">The addresses of the pages of the repository in the Web console.</param>
/// <param name="WrapDetails">Whether the tables are folded under a <c>details</c> element.</param>
/// <param name="ConfigInvalid">Whether the configuration file of the commit could not be read and was ignored.</param>
/// <remarks>
///   The composer reads nothing else: no repository, no option, no clock. What a section needs and the
///   comparison does not hold, the file counts and the failed uploads, is counted by the caller.
/// </remarks>
internal sealed record CommentContent(
  string Marker,
  string Title,
  CoverageConfig Config,
  Commit Head,
  PullRequest Pull,
  ComparisonContext Context,
  int HeadFileCount,
  int? BaseFileCount,
  int FailedUploadCount,
  WebLinks Links,
  bool WrapDetails,
  bool ConfigInvalid);
