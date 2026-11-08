// ------------------------------------------------------------------------------------------------
// <copyright file="CcFolderTabs.razor.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

using CodeCoverage.Web.Components.Ui.Models;

namespace CodeCoverage.Web.Components.Ui;

/// <summary>
///   The in-page tabs of a card (segmented pills, the active one in black), following the ARIA
///   tabs pattern.
/// </summary>
/// <remarks>
///   <para>
///     Only the active tab is a tab stop; the arrow keys, <c>Home</c> and <c>End</c> select and
///     focus another tab. The parent renders the panel of the active tab, and names it with
///     <see cref="PanelId" />.
///   </para>
///   <para>
///     Bound with <c>@bind-Value</c>, the parent owns the active tab; otherwise the component
///     starts from <see cref="Value" />, or the first tab, and keeps the selection itself.
///   </para>
/// </remarks>
public partial class CcFolderTabs
{
  #region Fields

  private readonly string _idPrefix = $"cc-tabs-{Guid.NewGuid():N}";

  private ElementReference[] _tabElements = [];

  private string? _activeId;

  private bool _focusPending;

  #endregion

  #region Parameters

  /// <summary>Gets or sets the tabs.</summary>
  [Parameter]
  public IReadOnlyList<CcTabItem> Tabs { get; set; } = [];

  /// <summary>Gets or sets the identifier of the active tab (supports <c>@bind-Value</c>).</summary>
  [Parameter]
  public string? Value { get; set; }

  /// <summary>Gets or sets the callback raised when another tab is selected.</summary>
  [Parameter]
  public EventCallback<string> ValueChanged { get; set; }

  /// <summary>Gets or sets the accessible name of the tab list.</summary>
  [Parameter]
  public string? AriaLabel { get; set; }

  /// <summary>Gets or sets the identifier of the panel the tabs control, if any.</summary>
  [Parameter]
  public string? PanelId { get; set; }

  /// <summary>Gets or sets the extra CSS classes of the tab list.</summary>
  [Parameter]
  public string? Class { get; set; }

  #endregion

  #region Properties

  /// <summary>Gets the identifier of the active tab.</summary>
  private string? ActiveId => _activeId;

  /// <summary>Gets the CSS classes of the tab list.</summary>
  private string CssClass => "cc-folder-tabs" + CcCss.Extra(Class);

  #endregion

  #region Lifecycle

  /// <inheritdoc />
  protected override void OnParametersSet()
  {
    if (_tabElements.Length != Tabs.Count)
    {
      _tabElements = new ElementReference[Tabs.Count];
    }

    // Controlled: the parent decides. Uncontrolled: the selection survives the parent renders.
    if (ValueChanged.HasDelegate || Tabs.All(t => t.Id != _activeId))
    {
      _activeId = Value ?? Tabs.FirstOrDefault()?.Id;
    }
  }

  /// <inheritdoc />
  protected override async Task OnAfterRenderAsync(bool firstRender)
  {
    if (!_focusPending)
    {
      return;
    }

    _focusPending = false;
    var index = IndexOf(_activeId);

    if (index >= 0)
    {
      await _tabElements[index].FocusAsync().ConfigureAwait(true);
    }
  }

  #endregion

  #region Methods

  private string TabId(CcTabItem tab) => $"{_idPrefix}-{tab.Id}";

  private int IndexOf(string? id)
  {
    for (var i = 0; i < Tabs.Count; i++)
    {
      if (Tabs[i].Id == id)
      {
        return i;
      }
    }

    return -1;
  }

  private Task OnKeyDownAsync(KeyboardEventArgs args, int index)
  {
    if (Tabs.Count == 0)
    {
      return Task.CompletedTask;
    }

    int? target = args.Key switch
    {
      "ArrowRight" => (index + 1) % Tabs.Count,
      "ArrowLeft" => (index - 1 + Tabs.Count) % Tabs.Count,
      "Home" => 0,
      "End" => Tabs.Count - 1,
      var _ => null,
    };

    return target is { } next ? SelectAsync(next, moveFocus: true) : Task.CompletedTask;
  }

  private async Task SelectAsync(int index, bool moveFocus)
  {
    var id = Tabs[index].Id;

    _focusPending = moveFocus;

    if (id == _activeId)
    {
      return;
    }

    _activeId = id;
    await ValueChanged.InvokeAsync(id).ConfigureAwait(true);
  }

  #endregion
}
