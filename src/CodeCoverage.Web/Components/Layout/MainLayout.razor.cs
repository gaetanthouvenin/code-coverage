// ------------------------------------------------------------------------------------------------
// <copyright file="MainLayout.razor.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Routing;

using CodeCoverage.Web.Components.Ui;
using CodeCoverage.Web.Mapping;
using CodeCoverage.Web.Messaging;
using CodeCoverage.Web.ViewModels.Access;

namespace CodeCoverage.Web.Components.Layout;

/// <summary>
///   The shell of every page: the <see cref="CcAppShell" /> with the top bar (brand, main
///   navigation, theme toggle), then the routed page inside a <see cref="CcErrorBoundary" />.
/// </summary>
/// <remarks>
///   The error boundary is recovered on every <see cref="NavigationManager.LocationChanged" />: after
///   a page failed to render, following any link shows the next page instead of the alert.
/// </remarks>
public partial class MainLayout : IDisposable
{
  #region Fields

  private CcErrorBoundary? _errorBoundary;

  #endregion

  #region Parameters

  /// <summary>Gets or sets the state of the authentication, cascaded by the router.</summary>
  [CascadingParameter]
  public Task<AuthenticationState>? AuthenticationState { get; set; }

  #endregion

  #region Injected services

  [Inject]
  private NavigationManager Navigation { get; set; } = null!;

  [Inject]
  private UiRequestRunner Runner { get; set; } = null!;

  #endregion

  #region Properties

  /// <summary>
  ///   Gets the navigation of the signed-in user, or <see langword="null" />: signed out, or its reading failed, the bar
  ///   keeps the entry of the repositories only.
  /// </summary>
  private NavigationViewModel? UserNavigation { get; set; }

  #endregion

  #region Lifecycle

  /// <inheritdoc />
  protected override async Task OnInitializedAsync()
  {
    Navigation.LocationChanged += OnLocationChanged;

    // The error pages render this layout too, possibly signed out: nothing is read without a session.
    if (AuthenticationState is null || (await AuthenticationState.ConfigureAwait(true)).User.Identity is not { IsAuthenticated: true })
    {
      return;
    }

    var result = await Runner.RunAsync(static (sender, ct) => sender.GetNavigationViewModelAsync(ct)).ConfigureAwait(true);

    UserNavigation = result.Succeeded ? result.Value : null;
  }

  #endregion

  #region Methods

  private void OnLocationChanged(object? sender, LocationChangedEventArgs e) =>
    _ = InvokeAsync(() => _errorBoundary?.Recover());

  #endregion

  #region Implementation of IDisposable

  /// <inheritdoc />
  public void Dispose()
  {
    Navigation.LocationChanged -= OnLocationChanged;
    GC.SuppressFinalize(this);
  }

  #endregion
}
