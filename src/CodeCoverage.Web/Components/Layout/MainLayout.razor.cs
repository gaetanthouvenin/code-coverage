// ------------------------------------------------------------------------------------------------
// <copyright file="MainLayout.razor.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.Extensions.Localization;

using CodeCoverage.Web.Components.Ui;
using CodeCoverage.Web.Components.Ui.Models;
using CodeCoverage.Web.Resources;

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

  #region Injected services

  [Inject]
  private NavigationManager Navigation { get; set; } = null!;

  [Inject]
  private IStringLocalizer<Common> L { get; set; } = null!;

  #endregion

  #region Properties

  /// <summary>Gets the entries of the main navigation.</summary>
  /// <remarks>The business sections join with their pages; the home page lists the repositories.</remarks>
  private IReadOnlyList<CcNavItem> NavItems { get; set; } = [];

  #endregion

  #region Lifecycle

  /// <inheritdoc />
  protected override void OnInitialized()
  {
    NavItems = [new CcNavItem(string.Empty, L["Nav_Repositories"])];
    Navigation.LocationChanged += OnLocationChanged;
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
