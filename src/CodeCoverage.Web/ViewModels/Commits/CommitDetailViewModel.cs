// ------------------------------------------------------------------------------------------------
// <copyright file="CommitDetailViewModel.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Web.ViewModels.Repositories;

namespace CodeCoverage.Web.ViewModels.Commits;

/// <summary>The summary of a commit: header, totals, flags, published statuses and configuration.</summary>
/// <param name="ShortSha">The seven first characters of its SHA.</param>
/// <param name="Title">The subject of its message, or its short SHA when its metadata are not cached.</param>
/// <param name="Branch">Its branch, or <see langword="null" />.</param>
/// <param name="Author">The login of its author, or <see langword="null" />.</param>
/// <param name="At">Its committer date, else when it was completed, else when it was first seen.</param>
/// <param name="PullNumber">The pull request it is the head of, or <see langword="null" />.</param>
/// <param name="Status">Its state.</param>
/// <param name="Coverage">Its coverage, formatted.</param>
/// <param name="Delta">Its change against the base of the push comparison, formatted with its sign, or <see langword="null" />.</param>
/// <param name="Trend">The direction of the change: positive, negative or zero.</param>
/// <param name="BaseSha">The short SHA of that base, or <see langword="null" />.</param>
/// <param name="BaseCoverage">The coverage of that base, formatted, or <see langword="null" />.</param>
/// <param name="Files">Its number of files, formatted.</param>
/// <param name="BaseFiles">The number of files of the base of the push comparison, formatted, or <see langword="null" /> without a base.</param>
/// <param name="Lines">Its number of lines, formatted.</param>
/// <param name="Hits">Its hits, formatted.</param>
/// <param name="Partials">Its partials, formatted.</param>
/// <param name="Misses">Its misses, formatted.</param>
/// <param name="Sessions">Its number of sessions.</param>
/// <param name="CarriedSessions">How many of its sessions were carried forward.</param>
/// <param name="CarriedOrigins">The flags and origin of each of its carried-forward sessions.</param>
/// <param name="Flags">Its flags, in ordinal order, without the project.</param>
/// <param name="Statuses">The statuses posted for it.</param>
/// <param name="Config">The configuration applied to it.</param>
/// <param name="Uploads">Its uploads by sequence, then the sessions carried forward to it by number.</param>
/// <param name="Format">How the repository writes a coverage, for the file tree read later.</param>
public sealed record CommitDetailViewModel(
  string ShortSha,
  string Title,
  string? Branch,
  string? Author,
  DateTimeOffset At,
  int? PullNumber,
  CommitStatus Status,
  string Coverage,
  string? Delta,
  int Trend,
  string? BaseSha,
  string? BaseCoverage,
  string Files,
  string? BaseFiles,
  string Lines,
  string Hits,
  string Partials,
  string Misses,
  int Sessions,
  int CarriedSessions,
  IReadOnlyList<CarriedOriginViewModel> CarriedOrigins,
  IReadOnlyList<CommitFlagRowViewModel> Flags,
  IReadOnlyList<PostedStatusViewModel> Statuses,
  CommitConfigViewModel Config,
  IReadOnlyList<UploadSessionRowViewModel> Uploads,
  CoverageFormatViewModel Format);
