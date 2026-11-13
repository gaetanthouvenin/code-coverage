// ------------------------------------------------------------------------------------------------
// <copyright file="PullRequestDetailViewModel.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Web.ViewModels.Comparisons;

/// <summary>A pull request and the comparison stored for its head.</summary>
/// <param name="Number">The number of the pull request.</param>
/// <param name="Title">Its title.</param>
/// <param name="Author">The login of its author, or <see langword="null" />.</param>
/// <param name="HeadBranch">The branch it merges.</param>
/// <param name="BaseBranch">The branch it merges into.</param>
/// <param name="CommentUrl">The address of the comment the console posted, on GitHub only, or <see langword="null" />.</param>
/// <param name="Comparison">The comparison of its head, or <see langword="null" /> while it is pending.</param>
public sealed record PullRequestDetailViewModel(
  int Number,
  string Title,
  string? Author,
  string HeadBranch,
  string BaseBranch,
  string? CommentUrl,
  ComparisonViewModel? Comparison);
