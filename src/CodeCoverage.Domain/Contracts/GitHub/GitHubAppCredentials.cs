// ------------------------------------------------------------------------------------------------
// <copyright file="GitHubAppCredentials.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Domain.Contracts.GitHub;

/// <summary>What GitHub gives once for an App created from a manifest; the secrets are in clear.</summary>
/// <param name="AppId">The identifier of the App.</param>
/// <param name="Slug">The slug of the App.</param>
/// <param name="ClientId">The client identifier of the App.</param>
/// <param name="ClientSecret">The client secret, in clear.</param>
/// <param name="WebhookSecret">The secret that signs the webhooks, in clear.</param>
/// <param name="PrivateKeyPem">The private key in PEM format, in clear.</param>
public sealed record GitHubAppCredentials(string AppId, string Slug, string ClientId, string ClientSecret, string WebhookSecret, string PrivateKeyPem)
{
  #region Methods

  /// <summary>Returns the public identifiers only: a secret never reaches a log or a trace.</summary>
  /// <returns>The name of the type and its public identifiers.</returns>
  public override string ToString() => $"{nameof(GitHubAppCredentials)} {{ {nameof(AppId)} = {AppId}, {nameof(Slug)} = {Slug} }}";

  #endregion
}
