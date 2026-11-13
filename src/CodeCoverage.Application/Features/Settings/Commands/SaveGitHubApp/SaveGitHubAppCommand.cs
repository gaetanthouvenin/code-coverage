// ------------------------------------------------------------------------------------------------
// <copyright file="SaveGitHubAppCommand.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using CodeCoverage.Application.Security;

namespace CodeCoverage.Application.Features.Settings.Commands.SaveGitHubApp;

/// <summary>Stores a GitHub App created by hand; authenticated by the setup token, in the handler.</summary>
/// <param name="SetupToken">The setup token, or <see langword="null" />.</param>
/// <param name="AppId">The numeric identifier of the App.</param>
/// <param name="ClientId">The client identifier of the App.</param>
/// <param name="ClientSecret">A client secret of the App.</param>
/// <param name="WebhookSecret">The secret the deliveries of the App are signed with.</param>
/// <param name="PrivateKeyPem">The private key of the App, in PEM.</param>
/// <param name="Slug">The slug of the App, which its public page uses, or <see langword="null" />.</param>
/// <remarks>
///   The manifest flow of <c>CompleteGitHubAppManifest</c> is the usual way in; this one serves an App created on the
///   forge by hand, or one moved from another instance. It refuses an instance that already has an App, so no
///   anonymous caller can ever replace it.
/// </remarks>
public sealed record SaveGitHubAppCommand(
  string? SetupToken,
  string AppId,
  string ClientId,
  string ClientSecret,
  string WebhookSecret,
  string PrivateKeyPem,
  string? Slug) : ICommand, IAnonymousEntry
{
  #region Methods

  /// <inheritdoc />
  /// <remarks>Never a secret nor the token: an instance can reach a log.</remarks>
  public override string ToString() => nameof(SaveGitHubAppCommand);

  #endregion
}
