// ------------------------------------------------------------------------------------------------
// <copyright file="GitSha.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Diagnostics.CodeAnalysis;

using CodeCoverage.Domain.Guards;

namespace CodeCoverage.Domain.Git;

/// <summary>The full SHA of a git object: a commit, a tree or a blob.</summary>
/// <remarks>
///   A value is always 40 hexadecimal characters in lower case, so two SHAs written in different cases are
///   equal and a value is safe to place in the address of a forge.
/// </remarks>
public sealed record GitSha
{
  #region Fields

  /// <summary>The length of a SHA, in characters.</summary>
  public const int Length = 40;

  #endregion

  #region Ctors

  /// <summary>Initializes a new instance of the <see cref="GitSha" /> class.</summary>
  /// <param name="value">The SHA, already checked and in lower case.</param>
  private GitSha(string value) => Value = value;

  #endregion

  #region Properties

  /// <summary>Gets the 40 hexadecimal characters of the SHA, in lower case.</summary>
  public string Value { get; }

  #endregion

  #region Methods

  /// <summary>Reads a full SHA, whatever its case.</summary>
  /// <param name="value">The text to read.</param>
  /// <returns>The SHA.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="value" /> is <see langword="null" />.</exception>
  /// <exception cref="ArgumentException">
  ///   <paramref name="value" /> is not made of exactly 40 hexadecimal characters.
  /// </exception>
  public static GitSha Parse(string value)
  {
    ArgumentNullException.ThrowIfNull(value);

    ArgumentException.ThrowIf(
      !TryParse(value, out var sha),
      $"A SHA is made of exactly {Length} hexadecimal characters.",
      nameof(value));

    return sha;
  }

  /// <summary>Tries to read a full SHA, whatever its case.</summary>
  /// <param name="value">The text to read, or <see langword="null" />.</param>
  /// <param name="sha">The SHA, or <see langword="null" /> when the text is not one.</param>
  /// <returns><see langword="true" /> when the text is a full SHA; otherwise, <see langword="false" />.</returns>
  public static bool TryParse([NotNullWhen(true)] string? value, [NotNullWhen(true)] out GitSha? sha)
  {
    if (value is not { Length: Length } || !value.All(char.IsAsciiHexDigit))
    {
      sha = null;

      return false;
    }

    sha = new GitSha(value.ToLowerInvariant());

    return true;
  }

  /// <inheritdoc />
  public override string ToString() => Value;

  #endregion
}
