// ------------------------------------------------------------------------------------------------
// <copyright file="PullRequestRowViewModel.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Web.ViewModels.PullRequests;

/// <summary>A row of the list of the pull requests of a repository.</summary>
/// <param name="Number">Its number on the forge.</param>
/// <param name="Title">Its title.</param>
/// <param name="Author">The login of its author, or <see langword="null" />.</param>
/// <param name="HeadBranch">The branch it merges.</param>
/// <param name="BaseBranch">The branch it merges into.</param>
/// <param name="State">Its state.</param>
/// <param name="Patch">The coverage of its patch, formatted, or <see langword="null" /> while no comparison is stored.</param>
/// <param name="Delta">The change of the project coverage, formatted with its sign, or <see langword="null" />.</param>
/// <param name="Trend">The direction of the change: positive, negative or zero.</param>
public sealed record PullRequestRowViewModel(
  int Number,
  string Title,
  string? Author,
  string HeadBranch,
  string BaseBranch,
  PullRequestStatus State,
  string? Patch,
  string? Delta,
  int Trend);
