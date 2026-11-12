// ------------------------------------------------------------------------------------------------
// <copyright file="CcDialog.razor.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace CodeCoverage.Web.Components.Ui;

/// <summary>A modal dialog over a scrim: a title, a content and its actions, closed by Escape or a click on the scrim.</summary>
/// <remarks>
///   Opened, it takes the focus (its first field, else the dialog itself) and keeps Tab inside; closed, it gives the
///   focus back to what opened it (<c>ccUi.openDialog</c> and <c>ccUi.closeDialog</c> of <c>cc-ui.js</c>).
/// </remarks>
public partial class CcDialog : IAsyncDisposable
{
  #region Fields

  private readonly string _titleId = $"cc-dialog-{Guid.NewGuid():N}";

  private ElementReference _panel;

  private bool _focused;

  #endregion

  #region Parameters

  /// <summary>Gets or sets a value indicating whether the dialog is shown.</summary>
  [Parameter]
  public bool Open { get; set; }

  /// <summary>Gets or sets the title of the dialog, which names it for assistive technologies.</summary>
  [Parameter, EditorRequired]
  public string Title { get; set; } = null!;

  /// <summary>Gets or sets the content of the dialog.</summary>
  [Parameter]
  public RenderFragment? ChildContent { get; set; }

  /// <summary>Gets or sets the buttons at the bottom of the dialog, the main one last.</summary>
  [Parameter]
  public RenderFragment? Actions { get; set; }

  /// <summary>Gets or sets what happens when the user dismisses the dialog: Escape or a click on the scrim.</summary>
  [Parameter]
  public EventCallback OnClose { get; set; }

  #endregion

  #region Injected services

  [Inject]
  private IJSRuntime JS { get; set; } = null!;

  #endregion

  #region Lifecycle

  /// <inheritdoc />
  protected override async Task OnAfterRenderAsync(bool firstRender)
  {
    if (Open && !_focused)
    {
      _focused = true;
      await InvokeAsync("ccUi.openDialog", _panel).ConfigureAwait(true);
    }
    else if (!Open && _focused)
    {
      _focused = false;
      await InvokeAsync("ccUi.closeDialog").ConfigureAwait(true);
    }
  }

  #endregion

  #region Methods

  /// <summary>Moves the focus back into the open dialog, to its first field: e.g. after a refusal disabled its focused button.</summary>
  /// <returns>A task that completes when the focus moved.</returns>
  public Task FocusAsync() => Open ? InvokeAsync("ccUi.focusDialog", _panel) : Task.CompletedTask;

  private async Task InvokeAsync(string identifier, params object?[] arguments)
  {
    try
    {
      await JS.InvokeVoidAsync(identifier, arguments).ConfigureAwait(true);
    }
    catch (JSException)
    {
      // The helper script is missing: the dialog still works, without moving the focus.
    }
    catch (JSDisconnectedException)
    {
      // The circuit is gone: there is no focus left to move.
    }
  }

  private Task OnScrimClickAsync() => OnClose.InvokeAsync();

  private Task OnKeyDownAsync(KeyboardEventArgs args) => args.Key == "Escape" ? OnClose.InvokeAsync() : Task.CompletedTask;

  #endregion

  #region Implementation of IAsyncDisposable

  /// <inheritdoc />
  public async ValueTask DisposeAsync()
  {
    // A page left while its dialog is open gives the focus back too.
    if (_focused)
    {
      _focused = false;
      await InvokeAsync("ccUi.closeDialog").ConfigureAwait(true);
    }

    GC.SuppressFinalize(this);
  }

  #endregion
}
