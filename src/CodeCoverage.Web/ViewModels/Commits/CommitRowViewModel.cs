// ------------------------------------------------------------------------------------------------
// <copyright file="CommitRowViewModel.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Web.ViewModels.Commits;

/// <summary>A row of the list of the commits of a repository.</summary>
/// <param name="Sha">The full SHA, for the address of the commit.</param>
/// <param name="ShortSha">The seven first characters of the SHA.</param>
/// <param name="Message">The first line of its message, or <see langword="null" /> when its metadata are not cached.</param>
/// <param name="Author">The login of its author, or <see langword="null" />.</param>
/// <param name="At">Its committer date, else when it was completed, else when it was first seen.</param>
/// <param name="Coverage">Its coverage, formatted.</param>
/// <param name="Delta">Its change against its parent on the branch, formatted with its sign, or <see langword="null" />.</param>
/// <param name="Trend">The direction of the change: positive, negative or zero.</param>
/// <param name="Status">Its state.</param>
/// <param name="Sessions">Its number of sessions.</param>
public sealed record CommitRowViewModel(
  string Sha,
  string ShortSha,
  string? Message,
  string? Author,
  DateTimeOffset At,
  string Coverage,
  string? Delta,
  int Trend,
  CommitStatus Status,
  int Sessions);
