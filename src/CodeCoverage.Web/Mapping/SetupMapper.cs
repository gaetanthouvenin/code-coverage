// ------------------------------------------------------------------------------------------------
// <copyright file="SetupMapper.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using CodeCoverage.Application.Features.Settings.Commands.CompleteGitHubAppManifest;
using CodeCoverage.Application.Features.Settings.Commands.CompleteSetup;
using CodeCoverage.Application.Features.Settings.Commands.SaveGitHubApp;
using CodeCoverage.Application.Features.Settings.Commands.SavePublicUrls;
using CodeCoverage.Application.Features.Settings.Queries.GetSetupState;
using CodeCoverage.Domain.Settings;
using CodeCoverage.Web.ViewModels.Settings;

namespace CodeCoverage.Web.Mapping;

/// <summary>Sends what the first-start wizard needs: the page holds no Domain type and no secret of its own.</summary>
internal static class SetupMapper
{
  #region Methods

  /// <summary>Reads what the wizard has done on this instance.</summary>
  /// <param name="sender">The request sender.</param>
  /// <param name="setupToken">The setup token.</param>
  /// <param name="cancellationToken">A token to cancel the reading.</param>
  /// <returns>The state of the setup.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="sender" /> is <see langword="null" />.</exception>
  public static async Task<SetupViewModel> GetSetupViewModelAsync(this ISender sender, string? setupToken, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(sender);

    var state = await sender.Send(new GetSetupStateQuery(setupToken), cancellationToken).ConfigureAwait(false);

    return new SetupViewModel(state.GitHubAppCreated, state.PublicUrlsConfigured, state.AdministratorsConfigured, state.WebPublicUrl, state.ApiPublicUrl);
  }

  /// <summary>Saves the public addresses of the hosts, before the App that carries them exists.</summary>
  /// <param name="sender">The request sender.</param>
  /// <param name="setupToken">The setup token.</param>
  /// <param name="webPublicUrl">The public address of the console.</param>
  /// <param name="apiPublicUrl">The public address of the Api.</param>
  /// <param name="cancellationToken">A token to cancel the request.</param>
  /// <returns><see langword="true" /> once the addresses are saved.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="sender" /> is <see langword="null" />.</exception>
  public static async Task<bool> SavePublicUrlsAsync(
    this ISender sender,
    string? setupToken,
    string webPublicUrl,
    string apiPublicUrl,
    CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(sender);

    await sender.Send(new SavePublicUrlsCommand(setupToken, webPublicUrl, apiPublicUrl), cancellationToken).ConfigureAwait(false);

    return true;
  }

  /// <summary>Saves a GitHub App created by hand on the forge.</summary>
  /// <param name="sender">The request sender.</param>
  /// <param name="setupToken">The setup token.</param>
  /// <param name="form">What the operator read on the page of the App.</param>
  /// <param name="cancellationToken">A token to cancel the request.</param>
  /// <returns><see langword="true" /> once the App is saved.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="sender" /> or <paramref name="form" /> is <see langword="null" />.</exception>
  public static async Task<bool> SaveGitHubAppAsync(this ISender sender, string? setupToken, ManualAppFormViewModel form, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(sender);
    ArgumentNullException.ThrowIfNull(form);

    var command = new SaveGitHubAppCommand(
      setupToken,
      form.AppId,
      form.ClientId,
      form.ClientSecret,
      form.WebhookSecret,
      form.PrivateKeyPem,
      string.IsNullOrWhiteSpace(form.Slug) ? null : form.Slug);

    await sender.Send(command, cancellationToken).ConfigureAwait(false);

    return true;
  }

  /// <summary>Stores the App GitHub created from the manifest, from the code of the return.</summary>
  /// <param name="sender">The request sender.</param>
  /// <param name="setupToken">The setup token.</param>
  /// <param name="code">The temporary code GitHub gave.</param>
  /// <param name="cancellationToken">A token to cancel the request.</param>
  /// <returns><see langword="true" /> once the App is stored.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="sender" /> is <see langword="null" />.</exception>
  public static async Task<bool> CompleteGitHubAppManifestAsync(this ISender sender, string? setupToken, string code, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(sender);

    await sender.Send(new CompleteGitHubAppManifestCommand(setupToken, code), cancellationToken).ConfigureAwait(false);

    return true;
  }

  /// <summary>Names the administrators of the instance and ends the setup.</summary>
  /// <param name="sender">The request sender.</param>
  /// <param name="setupToken">The setup token.</param>
  /// <param name="adminUserIds">The numeric identifiers of the administrators, separated by commas.</param>
  /// <param name="cancellationToken">A token to cancel the request.</param>
  /// <returns><see langword="true" /> once the setup is over.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="sender" /> is <see langword="null" />.</exception>
  public static async Task<bool> CompleteSetupAsync(this ISender sender, string? setupToken, string adminUserIds, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(sender);

    await sender.Send(new CompleteSetupCommand(setupToken, null, null, adminUserIds), cancellationToken).ConfigureAwait(false);

    return true;
  }

  /// <summary>Writes the manifest that creates the App of the instance in one click.</summary>
  /// <param name="appName">The name of the App, which the operator may change.</param>
  /// <param name="webPublicUrl">The public address of the console.</param>
  /// <param name="apiPublicUrl">The public address of the Api.</param>
  /// <returns>The manifest, as the form posted to GitHub carries it; empty when an address is not a URL.</returns>
  public static string BuildManifest(string appName, string? webPublicUrl, string? apiPublicUrl) =>
    Uri.TryCreate(webPublicUrl, UriKind.Absolute, out var web) && Uri.TryCreate(apiPublicUrl, UriKind.Absolute, out var api)
      ? GitHubAppManifest.ToJson(appName, web, api)
      : string.Empty;

  #endregion
}
