// ------------------------------------------------------------------------------------------------
// <copyright file="CcUserMenu.razor.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Localization;

using CodeCoverage.Web.Resources;

namespace CodeCoverage.Web.Components.Ui;

/// <summary>
///   The user of the top bar: an avatar with the initials in the blue gradient, the login and the
///   role.
/// </summary>
/// <remarks>
///   <para>Three forms, by the parameters given:</para>
///   <list type="bullet">
///     <item><see cref="Href" />: a link to the account page;</item>
///     <item>
///       <see cref="ChildContent" />: a disclosure button (<c>aria-expanded</c>) opening a panel
///       of links, closed by the button, <c>Escape</c> or a navigation;
///     </item>
///     <item>neither: a static identity.</item>
///   </list>
/// </remarks>
public partial class CcUserMenu : IDisposable
{
  #region Fields

  private readonly string _panelId = $"cc-user-menu-{Guid.NewGuid():N}";

  private bool _open;

  #endregion

  #region Parameters

  /// <summary>Gets or sets the login or the display name of the user.</summary>
  [Parameter, EditorRequired]
  public string UserName { get; set; } = null!;

  /// <summary>Gets or sets the localized role shown under the name (« Administrator »).</summary>
  [Parameter]
  public string? RoleLabel { get; set; }

  /// <summary>Gets or sets the initials of the avatar; <see langword="null" /> derives them from <see cref="UserName" />.</summary>
  [Parameter]
  public string? Initials { get; set; }

  /// <summary>Gets or sets the address of the account page; when set, the user is a link.</summary>
  [Parameter]
  public string? Href { get; set; }

  /// <summary>Gets or sets the links of the panel; when set (without <see cref="Href" />), the user opens a menu.</summary>
  [Parameter]
  public RenderFragment? ChildContent { get; set; }

  /// <summary>Gets or sets the extra CSS classes of the user menu.</summary>
  [Parameter]
  public string? Class { get; set; }

  #endregion

  #region Injected services

  [Inject]
  private NavigationManager Navigation { get; set; } = null!;

  [Inject]
  private IStringLocalizer<UiStrings> L { get; set; } = null!;

  #endregion

  #region Properties

  /// <summary>Gets the initials to render.</summary>
  private string EffectiveInitials => string.IsNullOrWhiteSpace(Initials) ? InitialsOf(UserName) : Initials;

  /// <summary>Gets the accessible name of the menu button.</summary>
  private string MenuLabel => $"{L["AccountMenu"]}, {UserName}";

  /// <summary>Gets the CSS classes of the user menu.</summary>
  private string CssClass => "cc-user-menu" + CcCss.Extra(Class);

  #endregion

  #region Lifecycle

  /// <inheritdoc />
  protected override void OnInitialized() =>
    Navigation.LocationChanged += OnLocationChanged;

  #endregion

  #region Methods

  /// <summary>Derives the initials of a user name.</summary>
  /// <param name="userName">The login or the display name.</param>
  /// <returns>
  ///   The first letters of the first two words (« Guillaume Thouvenin » gives « GT »), or the first
  ///   two letters of a single word (« gthouvenin » gives « GT »), in upper case; « ? » when empty.
  /// </returns>
  public static string InitialsOf(string? userName)
  {
    var words = (userName ?? string.Empty).Split([' ', '-', '_', '.'], StringSplitOptions.RemoveEmptyEntries);

    var initials = words.Length switch
    {
      0 => "?",
      1 => words[0][..Math.Min(2, words[0].Length)],
      var _ => string.Concat(words[0][0], words[1][0]),
    };

    return initials.ToUpperInvariant();
  }

  private void Toggle() => _open = !_open;

  private void OnKeyDown(KeyboardEventArgs args)
  {
    if (_open && args.Key == "Escape")
    {
      _open = false;
    }
  }

  private void OnLocationChanged(object? sender, LocationChangedEventArgs e)
  {
    if (!_open)
    {
      return;
    }

    _open = false;
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
