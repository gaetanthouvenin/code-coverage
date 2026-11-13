// ------------------------------------------------------------------------------------------------
// <copyright file="Setup.razor.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Security.Cryptography;

using Mediator;

using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;

using CodeCoverage.Web.Components.Ui.Models;
using CodeCoverage.Web.Components.Ui.Services;
using CodeCoverage.Web.Configuration.Setup;
using CodeCoverage.Web.Mapping;
using CodeCoverage.Web.Messaging;
using CodeCoverage.Web.Resources;
using CodeCoverage.Web.ViewModels.Settings;

namespace CodeCoverage.Web.Components.Pages;

/// <summary>
///   The first-start wizard: the public addresses of the hosts, the GitHub App (created from a manifest or entered by
///   hand), and the administrators of the instance.
/// </summary>
/// <remarks>
///   <para>
///     The page renders on the server alone, without a circuit: it reads the setup token from the protected cookie of
///     the wizard, which no circuit can reach, and every step is a form posted to this same address.
///   </para>
///   <para>
///     The round trip through GitHub carries a <c>state</c> this page draws and keeps in that cookie. The return is
///     accepted only when the <c>state</c> of the address is the one the cookie holds, and the cookie drops it at
///     once: a return replayed later is refused. Nothing but the <c>code</c> of GitHub is ever read from the address.
///   </para>
///   <para>
///     A missing or invalid token shows its own state: the page never sends the visitor to the sign-in, which the
///     redirection of the wizard would send back here.
///   </para>
/// </remarks>
public partial class Setup
{
  #region Fields

  /// <summary>Where a manifest is posted to create the App of a personal account.</summary>
  private const string PersonalManifestUrl = "https://github.com/settings/apps/new";

  /// <summary>The number of bytes of the <c>state</c> of a round trip.</summary>
  private const int StateBytes = 32;

  #endregion

  #region Parameters

  /// <summary>Gets or sets the context of the request, which carries the cookie of the wizard.</summary>
  [CascadingParameter]
  private HttpContext HttpContext { get; set; } = null!;

  /// <summary>Gets or sets the code GitHub gives when it comes back from the manifest.</summary>
  [SupplyParameterFromQuery(Name = "code")]
  private string? Code { get; set; }

  /// <summary>Gets or sets the state GitHub gives back, which must be the one of the cookie.</summary>
  [SupplyParameterFromQuery(Name = "state")]
  private string? ReturnedState { get; set; }

  /// <summary>Gets or sets the public addresses typed in the first step.</summary>
  [SupplyParameterFromForm(FormName = "urls")]
  private PublicUrlsFormViewModel? Urls { get; set; }

  /// <summary>Gets or sets what the operator read on the page of an App created by hand.</summary>
  [SupplyParameterFromForm(FormName = "manual-app")]
  private ManualAppFormViewModel? App { get; set; }

  /// <summary>Gets or sets the administrators of the instance.</summary>
  [SupplyParameterFromForm(FormName = "admins")]
  private AdministratorsFormViewModel? Admins { get; set; }

  /// <summary>Gets or sets the name given to the App the manifest creates.</summary>
  [SupplyParameterFromForm(FormName = "manifest-start", Name = "appName")]
  private string? AppName { get; set; }

  /// <summary>Gets or sets the organization the App is created on, or nothing for the personal account.</summary>
  [SupplyParameterFromForm(FormName = "manifest-start", Name = "appOrganization")]
  private string? AppOrganization { get; set; }

  #endregion

  #region Injected services

  [Inject]
  private UiRequestRunner Runner { get; set; } = null!;

  [Inject]
  private SetupCookie Cookie { get; set; } = null!;

  [Inject]
  private IToastService Toasts { get; set; } = null!;

  [Inject]
  private IStringLocalizer<Common> L { get; set; } = null!;

  #endregion

  #region Properties

  /// <summary>Gets the state of the setup, or <see langword="null" /> while the token is missing or invalid.</summary>
  private SetupViewModel? State { get; set; }

  /// <summary>Gets the message of the last refusal the wizard itself raised, if any.</summary>
  private string? Error { get; set; }

  /// <summary>Gets what the wizard shows about the last refusal: its own message, else the one of the server.</summary>
  private string? Refusal => Error ?? Toasts.Toasts.LastOrDefault(static t => t.Tone is CcTone.Danger)?.Message;

  /// <summary>Gets the manifest to post to GitHub, once the operator asked for it.</summary>
  private string? Manifest { get; set; }

  /// <summary>Gets the address the manifest is posted to, with the state of the round trip.</summary>
  private string? ManifestAction { get; set; }

  private string? Token { get; set; }

  #endregion

  #region Lifecycle

  /// <inheritdoc />
  protected override async Task OnInitializedAsync()
  {
    // A form post gives its own model back; the other forms start empty.
    Urls ??= new PublicUrlsFormViewModel();
    App ??= new ManualAppFormViewModel();
    Admins ??= new AdministratorsFormViewModel();

    var (token, state) = Cookie.Read(HttpContext);
    (Token, AppName) = (token, AppName ?? L["Setup_AppNameDefault"]);

    if (token is null)
    {
      return;
    }

    await ReadStateAsync().ConfigureAwait(true);

    if (State is not null && Code is { Length: > 0 } code)
    {
      await CompleteManifestAsync(code, state).ConfigureAwait(true);
    }
  }

  #endregion

  #region Methods

  private static string StepClass(bool done) => done ? "cc-stepper__step cc-stepper__step--done" : "cc-stepper__step";

  // Every step reads the state again: what the server stored is what the wizard shows.
  private async Task ReadStateAsync()
  {
    var token = Token;
    var result = await Runner
                      .RunAsync((sender, ct) => sender.GetSetupViewModelAsync(token, ct), reportUnauthenticated: true)
                      .ConfigureAwait(true);

    if (result.Unauthenticated)
    {
      // The token of the cookie is no longer the one of the instance: the wizard asks for it again.
      ForgetToken();

      return;
    }

    State = result.Succeeded ? result.Value : State;

    if (State is { } loaded && Urls is { WebPublicUrl.Length: 0 })
    {
      Urls = new PublicUrlsFormViewModel { WebPublicUrl = loaded.WebPublicUrl ?? string.Empty, ApiPublicUrl = loaded.ApiPublicUrl ?? string.Empty };
    }
  }

  // A step that is refused leaves the wizard where it was, with the reason of the refusal.
  private async Task RunAsync(Func<ISender, CancellationToken, Task<bool>> step)
  {
    Error = null;
    var result = await Runner.RunAsync(step, reportUnauthenticated: true).ConfigureAwait(true);

    if (result.Unauthenticated)
    {
      ForgetToken();

      return;
    }

    await ReadStateAsync().ConfigureAwait(true);
  }

  private void ForgetToken()
  {
    (State, Token) = (null, null);
    Cookie.Clear(HttpContext);
  }

  private Task SaveUrlsAsync()
  {
    var (token, urls) = (Token, Urls!);

    return RunAsync((sender, ct) => sender.SavePublicUrlsAsync(token, urls.WebPublicUrl, urls.ApiPublicUrl, ct));
  }

  private Task SaveAppAsync()
  {
    var (token, app) = (Token, App!);

    return RunAsync((sender, ct) => sender.SaveGitHubAppAsync(token, app, ct));
  }

  private Task CompleteAsync()
  {
    var (token, admins) = (Token, Admins!);

    return RunAsync((sender, ct) => sender.CompleteSetupAsync(token, admins.AdminUserIds, ct));
  }

  // The state of the round trip is drawn here and kept in the cookie: GitHub carries it back in the address, and
  // nothing else can produce one that matches.
  private Task StartManifestAsync()
  {
    var state = Convert.ToHexString(RandomNumberGenerator.GetBytes(StateBytes));

    Cookie.Write(HttpContext, Token!, state);
    Manifest = SetupMapper.BuildManifest(AppName ?? L["Setup_AppNameDefault"], Urls!.WebPublicUrl, Urls.ApiPublicUrl);

    var target = string.IsNullOrWhiteSpace(AppOrganization)
                   ? PersonalManifestUrl
                   : $"https://github.com/organizations/{Uri.EscapeDataString(AppOrganization.Trim())}/settings/apps/new";

    ManifestAction = $"{target}?state={Uri.EscapeDataString(state)}";

    return Task.CompletedTask;
  }

  // The return of GitHub is accepted once, and only with the state the cookie holds.
  private async Task CompleteManifestAsync(string code, string? expectedState)
  {
    if (expectedState is null || ReturnedState is not { Length: > 0 } returned || !CryptographicOperations.FixedTimeEquals(
          System.Text.Encoding.UTF8.GetBytes(expectedState),
          System.Text.Encoding.UTF8.GetBytes(returned)))
    {
      Error = L["Setup_InvalidState"];

      return;
    }

    // The state is spent: a replayed return finds none and is refused.
    Cookie.Write(HttpContext, Token!, state: null);
    var token = Token;

    await RunAsync((sender, ct) => sender.CompleteGitHubAppManifestAsync(token, code, ct)).ConfigureAwait(true);
  }

  #endregion
}
