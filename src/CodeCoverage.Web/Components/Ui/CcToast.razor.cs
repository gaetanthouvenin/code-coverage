// ------------------------------------------------------------------------------------------------
// <copyright file="CcToast.razor.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;

using CodeCoverage.Web.Components.Ui.Models;
using CodeCoverage.Web.Components.Ui.Services;
using CodeCoverage.Web.Resources;

namespace CodeCoverage.Web.Components.Ui;

/// <summary>
///   The stack of the toasts of the circuit, at the bottom right of the screen: each toast raised
///   through <see cref="IToastService" /> shows its icon, its title and its message, and goes away
///   after <see cref="Duration" /> or when the user closes it.
/// </summary>
/// <remarks>
///   The layout holds one instance. The stack is an <c>aria-live="polite"</c> region; a
///   <see cref="CcTone.Warning" /> or <see cref="CcTone.Danger" /> toast is <c>role="alert"</c>
///   (announced at once), the others <c>role="status"</c>.
/// </remarks>
public partial class CcToast : IDisposable
{
  #region Fields

  private readonly CancellationTokenSource _disposed = new();

  private readonly HashSet<Guid> _scheduled = [];

  #endregion

  #region Parameters

  /// <summary>Gets or sets how long a toast stays on screen. Defaults to 4.2 seconds, as in the mockup.</summary>
  [Parameter]
  public TimeSpan Duration { get; set; } = TimeSpan.FromMilliseconds(4200);

  #endregion

  #region Injected services

  [Inject]
  private IToastService Toasts { get; set; } = null!;

  [Inject]
  private IStringLocalizer<UiStrings> L { get; set; } = null!;

  #endregion

  #region Lifecycle

  /// <inheritdoc />
  protected override void OnInitialized()
  {
    Toasts.Changed += OnToastsChanged;
    ScheduleDismissals();
  }

  #endregion

  #region Methods

  /// <summary>Gets the ARIA live-region role of a toast.</summary>
  /// <param name="toast">The toast.</param>
  /// <returns><c>alert</c> for a warning or a danger, <c>status</c> otherwise.</returns>
  private static string Role(CcToastMessage toast) =>
    toast.Tone is CcTone.Warning or CcTone.Danger ? "alert" : "status";

  private void OnToastsChanged(object? sender, EventArgs e) =>
    _ = InvokeAsync(() =>
    {
      ScheduleDismissals();
      StateHasChanged();
    });

  // Starts the timer of every toast shown since the last change; the timers stop with the stack.
  private void ScheduleDismissals()
  {
    var shown = Toasts.Toasts;

    foreach (var toast in shown)
    {
      if (_scheduled.Add(toast.Id))
      {
        _ = DismissLaterAsync(toast);
      }
    }

    _scheduled.IntersectWith(shown.Select(t => t.Id));
  }

  private async Task DismissLaterAsync(CcToastMessage toast)
  {
    try
    {
      await Task.Delay(Duration, _disposed.Token).ConfigureAwait(true);
      Toasts.Dismiss(toast);
    }
    catch (OperationCanceledException)
    {
      // The stack is gone with its circuit: nothing left to dismiss.
    }
  }

  #endregion

  #region Implementation of IDisposable

  /// <inheritdoc />
  public void Dispose()
  {
    Toasts.Changed -= OnToastsChanged;
    _disposed.Cancel();
    _disposed.Dispose();
    GC.SuppressFinalize(this);
  }

  #endregion
}
