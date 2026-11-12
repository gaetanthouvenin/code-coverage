// ------------------------------------------------------------------------------------------------
// <copyright file="SignIn.razor.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Localization;

using CodeCoverage.Web.Configuration.Authentication;
using CodeCoverage.Web.Mapping;
using CodeCoverage.Web.Messaging;
using CodeCoverage.Web.Resources;
using CodeCoverage.Web.ViewModels.Access;

namespace CodeCoverage.Web.Components.Pages;

/// <summary>The sign-in page: one button that starts the sign-in with GitHub (<c>cc-signin</c> of the mockup).</summary>
/// <remarks>
///   <para>
///     The page says why it is shown: a session that ended (<c>?expired=true</c>, set when a request finds the session
///     gone) or a sign-in that failed (<c>?error=true</c>). An account that reaches no repository is sent to the
///     access-denied page instead, with no session.
///   </para>
///   <para>
///     A signed-in user sees who it is signed in as, with a way back and a way out: the page never starts a second
///     sign-in on its own.
///   </para>
/// </remarks>
public partial class SignIn
{
  #region Parameters

  /// <summary>Gets or sets a value indicating whether the session of the user ended.</summary>
  [SupplyParameterFromQuery(Name = "expired")]
  public bool Expired { get; set; }

  /// <summary>Gets or sets a value indicating whether the last sign-in failed.</summary>
  [SupplyParameterFromQuery(Name = "error")]
  public bool Failed { get; set; }

  /// <summary>Gets or sets the address to return to after the sign-in.</summary>
  [SupplyParameterFromQuery(Name = AuthenticationEndpoints.ReturnUrlParameter)]
  public string? ReturnUrl { get; set; }

  /// <summary>Gets or sets the authentication state of the page, cascaded by the host.</summary>
  [CascadingParameter]
  public Task<AuthenticationState>? AuthenticationState { get; set; }

  #endregion

  #region Injected services

  [Inject]
  private IStringLocalizer<Common> L { get; set; } = null!;

  [Inject]
  private IStringLocalizer<UiStrings> U { get; set; } = null!;

  [Inject]
  private UiRequestRunner Runner { get; set; } = null!;

  #endregion

  #region Properties

  /// <summary>Gets the signed-in user, or <see langword="null" />.</summary>
  private CurrentUserViewModel? CurrentUser { get; set; }

  /// <summary>Gets the address that starts the sign-in, carrying where to return.</summary>
  private string ChallengeHref =>
    $"{AuthenticationEndpoints.ChallengePath.TrimStart('/')}?{AuthenticationEndpoints.ReturnUrlParameter}={Uri.EscapeDataString(ReturnHref)}";

  /// <summary>Gets the address to return to, kept on this site.</summary>
  private string ReturnHref => AuthenticationEndpoints.LocalOrHome(ReturnUrl);

  #endregion

  #region Lifecycle

  protected override async Task OnInitializedAsync()
  {
    // An ended session asks for a new sign-in: the session it may still carry is not shown again.
    if (Expired || AuthenticationState is null || (await AuthenticationState.ConfigureAwait(true)).User.Identity is not { IsAuthenticated: true })
    {
      return;
    }

    var result = await Runner.RunAsync(static (sender, ct) => sender.GetCurrentUserViewModelAsync(ct)).ConfigureAwait(true);

    CurrentUser = result.Succeeded ? result.Value : null;
  }

  #endregion
}
