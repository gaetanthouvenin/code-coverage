// ------------------------------------------------------------------------------------------------
// <copyright file="CcPillTabs.razor.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;

using CodeCoverage.Web.Components.Ui.Models;

namespace CodeCoverage.Web.Components.Ui;

/// <summary>
///   The second-level navigation under a page title (the sections of a repository or of the
///   administration): links in a white pill, the current one in black.
/// </summary>
/// <remarks>
///   The link matching the current address gets <c>aria-current="page"</c>; the component renders
///   again on every navigation.
/// </remarks>
public partial class CcPillTabs : IDisposable
{
  #region Parameters

  /// <summary>Gets or sets the links.</summary>
  [Parameter]
  public IReadOnlyList<CcNavItem> Items { get; set; } = [];

  /// <summary>Gets or sets the localized accessible name of the navigation (« Repository sections »).</summary>
  [Parameter, EditorRequired]
  public string AriaLabel { get; set; } = null!;

  /// <summary>Gets or sets the extra CSS classes of the navigation.</summary>
  [Parameter]
  public string? Class { get; set; }

  #endregion

  #region Injected services

  [Inject]
  private NavigationManager Navigation { get; set; } = null!;

  #endregion

  #region Properties

  /// <summary>Gets the CSS classes of the navigation.</summary>
  private string CssClass => "cc-pill-tabs" + CcCss.Extra(Class);

  #endregion

  #region Lifecycle

  /// <inheritdoc />
  protected override void OnInitialized() =>
    Navigation.LocationChanged += OnLocationChanged;

  #endregion

  #region Methods

  private void OnLocationChanged(object? sender, LocationChangedEventArgs e) =>
    _ = InvokeAsync(StateHasChanged);

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
