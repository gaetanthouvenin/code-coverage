// ------------------------------------------------------------------------------------------------
// <copyright file="CreateUploadTokenRequest.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Api.Contracts.Tokens;

/// <summary>Asks for an upload token, generated or imported from Codecov.</summary>
/// <param name="Label">The name given to the token.</param>
/// <param name="ImportedValue">The value of a <c>CODECOV_TOKEN</c> to import, or <see langword="null" /> to generate one.</param>
/// <param name="ExpiresAt">When it expires, or <see langword="null" />.</param>
public sealed record CreateUploadTokenRequest(string Label, string? ImportedValue, DateTimeOffset? ExpiresAt)
{
  #region Methods

  /// <inheritdoc />
  /// <remarks>Never the imported value: an instance can reach a log.</remarks>
  public override string ToString() => $"{nameof(CreateUploadTokenRequest)} {{ Label = {Label} }}";

  #endregion
}
