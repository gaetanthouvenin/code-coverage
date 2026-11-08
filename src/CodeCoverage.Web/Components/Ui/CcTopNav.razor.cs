// ------------------------------------------------------------------------------------------------
// <copyright file="CcTopNav.razor.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;

using CodeCoverage.Web.Components.Ui.Models;
using CodeCoverage.Web.Resources;

namespace CodeCoverage.Web.Components.Ui;

/// <summary>
///   The top bar of the application, a white pill: the brand on the left, the main navigation in
///   the middle (the current entry in a black pill), the icon actions, the theme toggle and the
///   user menu on the right.
/// </summary>
/// <remarks>
///   <para>
///     Below 1040 px the navigation moves to a drop-down panel opened by the menu button
///     (<c>aria-expanded</c>, <c>aria-controls</c>); the panel closes on every navigation.
///   </para>
///   <para>
///     The theme toggle switches <c>data-theme</c> on the document through <c>ccUi.toggleTheme</c>
///     (<c>wwwroot/js/cc-ui.js</c>), which remembers the choice in the browser.
///   </para>
/// </remarks>
public partial class CcTopNav : IDisposable
{
  #region Fields

  private readonly string _mobileId = $"cc-mobile-nav-{Guid.NewGuid():N}";

  private bool _menuOpen;

  private bool _isDark;

  #endregion

  #region Parameters

  /// <summary>Gets or sets the entries of the main navigation.</summary>
  [Parameter]
  public IReadOnlyList<CcNavItem> Items { get; set; } = [];

  /// <summary>Gets or sets the product name next to the brand mark; <see langword="null" /> uses « Coverage ».</summary>
  [Parameter]
  public string? ProductName { get; set; }

  /// <summary>Gets or sets the address of the brand link. Defaults to the home page.</summary>
  [Parameter]
  public string HomeHref { get; set; } = "./";

  /// <summary>Gets or sets the icon actions shown before the theme toggle (notifications…).</summary>
  [Parameter]
  public RenderFragment? Actions { get; set; }

  /// <summary>Gets or sets the user menu shown at the right end of the bar.</summary>
  [Parameter]
  public RenderFragment? UserMenu { get; set; }

  /// <summary>Gets or sets a value indicating whether the theme toggle is shown. Defaults to <see langword="true" />.</summary>
  [Parameter]
  public bool ShowThemeToggle { get; set; } = true;

  /// <summary>Gets or sets the extra CSS classes of the top bar.</summary>
  [Parameter]
  public string? Class { get; set; }

  #endregion

  #region Injected services

  [Inject]
  private NavigationManager Navigation { get; set; } = null!;

  [Inject]
  private IJSRuntime JS { get; set; } = null!;

  [Inject]
  private IStringLocalizer<UiStrings> L { get; set; } = null!;

  #endregion

  #region Properties

  /// <summary>Gets the product name to render.</summary>
  private string EffectiveProductName => string.IsNullOrEmpty(ProductName) ? L["ProductName"] : ProductName;

  /// <summary>Gets the accessible name of the brand link.</summary>
  private string HomeLabel => L["Home", EffectiveProductName];

  /// <summary>Gets the accessible name of the theme toggle, which names the next theme.</summary>
  private string ThemeLabel => _isDark ? L["ThemeToLight"] : L["ThemeToDark"];

  /// <summary>Gets the CSS classes of the top bar.</summary>
  private string CssClass => "cc-topnav" + CcCss.Extra(Class);

  #endregion

  #region Lifecycle

  /// <inheritdoc />
  protected override void OnInitialized() =>
    Navigation.LocationChanged += OnLocationChanged;

  /// <inheritdoc />
  protected override async Task OnAfterRenderAsync(bool firstRender)
  {
    // The server cannot know the theme of the browser: read it once the circuit is up.
    if (!firstRender || !ShowThemeToggle)
    {
      return;
    }

    try
    {
      _isDark = await JS.InvokeAsync<bool>("ccUi.isDark").ConfigureAwait(true);
      StateHasChanged();
    }
    catch (JSException)
    {
      // The helper script is missing: the toggle keeps its light-theme label.
    }
    catch (JSDisconnectedException)
    {
      // The circuit is gone: nothing left to render.
    }
  }

  #endregion

  #region Methods

  private void ToggleMenu() => _menuOpen = !_menuOpen;

  private async Task ToggleThemeAsync() =>
    _isDark = await JS.InvokeAsync<bool>("ccUi.toggleTheme").ConfigureAwait(true);

  private void OnLocationChanged(object? sender, LocationChangedEventArgs e)
  {
    _menuOpen = false;
    _ = InvokeAsync(StateHasChanged);
  }

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
