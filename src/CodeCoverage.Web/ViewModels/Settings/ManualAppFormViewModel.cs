// ------------------------------------------------------------------------------------------------
// <copyright file="ManualAppFormViewModel.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Web.ViewModels.Settings;

/// <summary>What the operator reads on the page of a GitHub App created by hand.</summary>
/// <remarks>
///   The names of the properties are those of the command, so a refusal of the server shows under the right field.
///   An instance holds secrets only while the form is posted, and never writes them down.
/// </remarks>
public sealed class ManualAppFormViewModel
{
  #region Properties

  /// <summary>Gets or sets the numeric identifier of the App.</summary>
  public string AppId { get; set; } = string.Empty;

  /// <summary>Gets or sets the client identifier of the App.</summary>
  public string ClientId { get; set; } = string.Empty;

  /// <summary>Gets or sets a client secret of the App.</summary>
  public string ClientSecret { get; set; } = string.Empty;

  /// <summary>Gets or sets the secret the deliveries of the App are signed with.</summary>
  public string WebhookSecret { get; set; } = string.Empty;

  /// <summary>Gets or sets the private key of the App, in PEM.</summary>
  public string PrivateKeyPem { get; set; } = string.Empty;

  /// <summary>Gets or sets the slug of the App, which its public page uses.</summary>
  public string Slug { get; set; } = string.Empty;

  #endregion

  #region Methods

  /// <inheritdoc />
  /// <remarks>Never a secret: an instance can reach a log.</remarks>
  public override string ToString() => $"{nameof(ManualAppFormViewModel)} {{ AppId = {AppId} }}";

  #endregion
}
