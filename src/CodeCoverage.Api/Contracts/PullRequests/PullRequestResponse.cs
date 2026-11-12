// ------------------------------------------------------------------------------------------------
// <copyright file="PullRequestResponse.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Api.Contracts.PullRequests;

/// <summary>A pull request, with what the comparison of its head gives.</summary>
/// <param name="Number">Its number.</param>
/// <param name="Title">Its title.</param>
/// <param name="Author">The login of its author, or <see langword="null" />.</param>
/// <param name="State">Its state: open, closed or merged.</param>
/// <param name="HeadSha">The full SHA of its head.</param>
/// <param name="HeadBranch">The branch of its head.</param>
/// <param name="BaseBranch">The branch it targets.</param>
/// <param name="UpdatedAt">When it was last updated, in UTC.</param>
/// <param name="HeadCoverage">The coverage of its head in percent, or <see langword="null" /> while not compared.</param>
/// <param name="PatchCoverage">
///   The coverage of its changed lines in percent, or <see langword="null" /> while not compared or without a coverable
///   changed line.
/// </param>
/// <param name="Delta">The change of the project coverage in points, or <see langword="null" />.</param>
public sealed record PullRequestResponse(
  int Number,
  string Title,
  string? Author,
  string State,
  string HeadSha,
  string HeadBranch,
  string BaseBranch,
  DateTimeOffset UpdatedAt,
  decimal? HeadCoverage,
  decimal? PatchCoverage,
  decimal? Delta);
