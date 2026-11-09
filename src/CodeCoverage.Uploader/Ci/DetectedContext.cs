// ------------------------------------------------------------------------------------------------
// <copyright file="DetectedContext.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Globalization;

namespace CodeCoverage.Uploader.Ci;

/// <summary>What the uploader knows about the build it runs in.</summary>
/// <remarks>
///   A value that cannot be trusted is left <see langword="null" />, with a warning: a wrong commit
///   would silently corrupt every result, whereas a missing one only loses this upload.
/// </remarks>
internal sealed record DetectedContext
{
  #region Fields

  /// <summary>The prefix of the ref of a branch.</summary>
  public const string BranchRefPrefix = "refs/heads/";

  private const int ShaLength = 40;

  #endregion

  #region Properties

  /// <summary>Gets the CI service, one of the values of the <c>ciService</c> upload field.</summary>
  public required string Service { get; init; }

  /// <summary>Gets the <c>owner/repo</c> slug, or <see langword="null" /> when unknown.</summary>
  public string? Slug { get; init; }

  /// <summary>Gets the commit in lower case, or <see langword="null" /> when unknown.</summary>
  public string? Sha { get; init; }

  /// <summary>Gets the head branch, or <see langword="null" /> when there is none.</summary>
  public string? Branch { get; init; }

  /// <summary>Gets the pull request number, or <see langword="null" /> outside a pull request.</summary>
  public int? PullRequest { get; init; }

  /// <summary>Gets the address of the build, or <see langword="null" /> when unknown.</summary>
  public string? BuildUrl { get; init; }

  /// <summary>Gets the identifier of the build, or <see langword="null" /> when unknown.</summary>
  public string? BuildId { get; init; }

  /// <summary>Gets the identifier of the job, or <see langword="null" /> when unknown.</summary>
  public string? JobId { get; init; }

  /// <summary>Gets the root of the repository, or <see langword="null" /> when unknown.</summary>
  public string? Root { get; init; }

  /// <summary>Gets why the commit is unknown, until the options have had their say.</summary>
  public string? ShaFailure { get; init; }

  /// <summary>Gets why the slug is unknown, until the options have had their say.</summary>
  public string? SlugFailure { get; init; }

  /// <summary>Gets what the build must be told about the detection.</summary>
  public IReadOnlyList<string> Warnings { get; init; } = [];

  #endregion

  #region Methods

  /// <summary>Reads a full SHA, the only form the server accepts.</summary>
  /// <param name="value">The text to read, or <see langword="null" />.</param>
  /// <returns>The SHA in lower case, or <see langword="null" /> when the text is not one.</returns>
  public static string? NormalizeSha(string? value)
  {
    return value is { Length: ShaLength } && value.All(char.IsAsciiHexDigit)
             ? value.ToLowerInvariant()
             : null;
  }

  /// <summary>Reads a pull request number.</summary>
  /// <param name="value">The text to read, or <see langword="null" />.</param>
  /// <returns>The number, or <see langword="null" /> when the text is not a positive integer.</returns>
  public static int? ParseNumber(string? value)
  {
    return int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var number)
           && number > 0
             ? number
             : null;
  }

  /// <summary>Removes the prefix of a branch ref, when the text has it.</summary>
  /// <param name="value">A branch ref or a branch name, or <see langword="null" />.</param>
  /// <returns>The branch name, or <see langword="null" /> when <paramref name="value" /> is.</returns>
  public static string? StripBranchRef(string? value)
  {
    return value is not null && value.StartsWith(BranchRefPrefix, StringComparison.Ordinal)
             ? value[BranchRefPrefix.Length..]
             : value;
  }

  #endregion
}
