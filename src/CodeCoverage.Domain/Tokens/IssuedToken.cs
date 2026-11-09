// ------------------------------------------------------------------------------------------------
// <copyright file="IssuedToken.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Domain.Tokens;

/// <summary>A token that was just created, with its value in clear, returned this one time.</summary>
public sealed class IssuedToken
{
  #region Ctors

  /// <summary>Initializes a new instance of the <see cref="IssuedToken" /> class.</summary>
  /// <param name="token">The saved token.</param>
  /// <param name="value">Its value in clear.</param>
  /// <exception cref="ArgumentNullException"><paramref name="token" /> is <see langword="null" />.</exception>
  /// <exception cref="ArgumentException"><paramref name="value" /> is empty or white space.</exception>
  public IssuedToken(UploadToken token, string value)
  {
    ArgumentNullException.ThrowIfNull(token);
    ArgumentException.ThrowIfNullOrWhiteSpace(value);

    Token = token;
    Value = value;
  }

  #endregion

  #region Properties

  /// <summary>Gets the saved token.</summary>
  public UploadToken Token { get; }

  /// <summary>Gets its value in clear.</summary>
  public string Value { get; }

  #endregion

  #region Methods

  /// <inheritdoc />
  /// <remarks>Never the value: an instance can reach a log.</remarks>
  public override string ToString() => $"{Token.Hint}…(redacted)";

  #endregion
}
