// ------------------------------------------------------------------------------------------------
// <copyright file="UploadRequest.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Uploader.Http;

/// <summary>What one upload sends to the server.</summary>
internal sealed record UploadRequest
{
  #region Properties

  /// <summary>Gets the <c>owner/repo</c> slug.</summary>
  public required string Slug { get; init; }

  /// <summary>Gets the commit.</summary>
  public required string Sha { get; init; }

  /// <summary>Gets the head branch, or <see langword="null" />.</summary>
  public string? Branch { get; init; }

  /// <summary>Gets the pull request number, or <see langword="null" />.</summary>
  public int? PullRequest { get; init; }

  /// <summary>Gets the parent commit, or <see langword="null" />.</summary>
  public string? ParentSha { get; init; }

  /// <summary>Gets the flags.</summary>
  public IReadOnlyList<string> Flags { get; init; } = [];

  /// <summary>Gets the session name, or <see langword="null" />.</summary>
  public string? Name { get; init; }

  /// <summary>Gets the CI service, or <see langword="null" />.</summary>
  public string? CiService { get; init; }

  /// <summary>Gets the address of the build, or <see langword="null" />.</summary>
  public string? BuildUrl { get; init; }

  /// <summary>Gets the identifier of the build, or <see langword="null" />.</summary>
  public string? BuildId { get; init; }

  /// <summary>Gets the identifier of the job, or <see langword="null" />.</summary>
  public string? JobId { get; init; }

  /// <summary>Gets the root of the repository on the agent, or <see langword="null" />.</summary>
  public string? Root { get; init; }

  /// <summary>Gets the version of the uploader, or <see langword="null" />.</summary>
  public string? UploaderVersion { get; init; }

  /// <summary>Gets the files of the repository, or <see langword="null" /> to leave them out.</summary>
  public IReadOnlyList<string>? Network { get; init; }

  /// <summary>Gets the full paths of the reports.</summary>
  public required IReadOnlyList<string> Reports { get; init; }

  #endregion
}
