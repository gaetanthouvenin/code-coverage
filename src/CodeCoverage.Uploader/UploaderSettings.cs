// ------------------------------------------------------------------------------------------------
// <copyright file="UploaderSettings.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Uploader;

/// <summary>The options of one run, from the command line or the environment.</summary>
/// <remarks>
///   A class rather than a record: the generated <c>ToString</c> of a record would print the token.
/// </remarks>
internal sealed class UploaderSettings
{
  #region Properties

  /// <summary>Gets the address of the CodeCoverage server.</summary>
  public required Uri Url { get; init; }

  /// <summary>Gets the upload credential, or <see langword="null" /> when none is given.</summary>
  public string? Token { get; init; }

  /// <summary>Gets the globs or paths of the reports; empty to search for them.</summary>
  public IReadOnlyList<string> Files { get; init; } = [];

  /// <summary>Gets the flags of the upload.</summary>
  public IReadOnlyList<string> Flags { get; init; } = [];

  /// <summary>Gets the session name, or <see langword="null" /> when none is given.</summary>
  public string? Name { get; init; }

  /// <summary>Gets the <c>owner/repo</c> slug, or <see langword="null" /> to detect it.</summary>
  public string? Slug { get; init; }

  /// <summary>Gets the commit, or <see langword="null" /> to detect it.</summary>
  public string? Sha { get; init; }

  /// <summary>Gets the head branch, or <see langword="null" /> to detect it.</summary>
  public string? Branch { get; init; }

  /// <summary>Gets the pull request number, or <see langword="null" /> to detect it.</summary>
  public int? PullRequest { get; init; }

  /// <summary>Gets the parent commit, or <see langword="null" /> to detect it.</summary>
  public string? ParentSha { get; init; }

  /// <summary>Gets the repository root, or <see langword="null" /> to detect it.</summary>
  public string? Root { get; init; }

  /// <summary>Gets a value indicating whether the search for reports is disabled.</summary>
  public bool DisableSearch { get; init; }

  /// <summary>Gets a value indicating whether the file list of the repository is left out.</summary>
  public bool NoNetwork { get; init; }

  /// <summary>Gets a value indicating whether to wait for the processing of the upload.</summary>
  public bool Wait { get; init; }

  /// <summary>Gets a value indicating whether to declare the uploads complete.</summary>
  public bool Finalize { get; init; }

  /// <summary>Gets a value indicating whether to print what would be sent, only.</summary>
  public bool DryRun { get; init; }

  /// <summary>Gets a value indicating whether a failure must fail the build.</summary>
  public bool FailOnError { get; init; }

  /// <summary>Gets a value indicating whether to print the details of the run.</summary>
  public bool Verbose { get; init; }

  #endregion
}
