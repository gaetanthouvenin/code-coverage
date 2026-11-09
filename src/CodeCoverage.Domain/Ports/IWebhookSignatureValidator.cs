// ------------------------------------------------------------------------------------------------
// <copyright file="IWebhookSignatureValidator.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Domain.Ports;

/// <summary>Checks that a webhook delivery was signed with the secret of the GitHub App.</summary>
public interface IWebhookSignatureValidator
{
  #region Methods

  /// <summary>Checks the signature of a body against the current secret and the previous one.</summary>
  /// <param name="payload">The raw body.</param>
  /// <param name="signature">The value of the <c>X-Hub-Signature-256</c> header, or <see langword="null" />.</param>
  /// <returns><see langword="true" /> when one of the secrets signed this body.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="payload" /> is <see langword="null" />.</exception>
  bool IsValid(byte[] payload, string? signature);

  #endregion
}
