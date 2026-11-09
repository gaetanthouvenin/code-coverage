// ------------------------------------------------------------------------------------------------
// <copyright file="UploadCredential.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Text.Json.Serialization;

using CodeCoverage.Domain.Guards;

namespace CodeCoverage.Domain.Contracts.Uploads;

/// <summary>The credential presented with an upload, whose value never reaches a log or a serialized form.</summary>
public sealed class UploadCredential
{
  #region Fields

  /// <summary>The prefix of the generated tokens, the only part of a value that may be shown.</summary>
  private const string GeneratedPrefix = "cvc_";

  /// <summary>How many characters of a generated token may be shown.</summary>
  private const int ShownLength = 8;

  #endregion

  #region Ctors

  /// <summary>Initializes a new instance of the <see cref="UploadCredential" /> class.</summary>
  /// <param name="kind">What the credential is.</param>
  /// <param name="value">Its value.</param>
  /// <exception cref="ArgumentOutOfRangeException"><paramref name="kind" /> is not a defined value.</exception>
  /// <exception cref="ArgumentException"><paramref name="value" /> is empty or white space.</exception>
  public UploadCredential(UploadCredentialKind kind, string value)
  {
    ArgumentOutOfRangeException.ThrowIfUndefined(kind);
    ArgumentException.ThrowIfNullOrWhiteSpace(value);

    Kind = kind;
    Value = value;
  }

  #endregion

  #region Properties

  /// <summary>Gets what the credential is.</summary>
  public UploadCredentialKind Kind { get; }

  /// <summary>Gets its value.</summary>
  [JsonIgnore]
  public string Value { get; }

  #endregion

  #region Methods

  /// <inheritdoc />
  /// <remarks>Shows the start of a generated token only; an imported token or a JWT shows nothing.</remarks>
  public override string ToString() =>
    Kind is UploadCredentialKind.UploadToken && Value.StartsWith(GeneratedPrefix, StringComparison.Ordinal) && Value.Length > ShownLength
      ? $"{Value[..ShownLength]}…(redacted)"
      : "(redacted)";

  #endregion
}
