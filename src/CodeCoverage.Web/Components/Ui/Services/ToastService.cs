// ------------------------------------------------------------------------------------------------
// <copyright file="ToastService.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Web.Components.Ui.Models;

namespace CodeCoverage.Web.Components.Ui.Services;

/// <summary>The toasts of a circuit, kept in memory until they are dismissed.</summary>
/// <remarks>
///   Thread-safe: a request may complete on a thread-pool thread. At most
///   <see cref="MaxToasts" /> toasts are kept; a new one pushes the oldest out.
/// </remarks>
internal sealed class ToastService : IToastService
{
  #region Fields

  /// <summary>The number of toasts shown at most at the same time.</summary>
  public const int MaxToasts = 5;

  private readonly Lock _gate = new();

  private readonly List<CcToastMessage> _toasts = [];

  #endregion

  #region Properties

  /// <inheritdoc />
  public IReadOnlyList<CcToastMessage> Toasts
  {
    get
    {
      lock (_gate)
      {
        return [.. _toasts];
      }
    }
  }

  #endregion

  #region Methods

  /// <inheritdoc />
  public CcToastMessage Show(CcTone tone, string title, string? message = null)
  {
    ArgumentException.ThrowIfNullOrWhiteSpace(title);

    var toast = new CcToastMessage(tone, title, string.IsNullOrWhiteSpace(message) ? null : message);

    lock (_gate)
    {
      _toasts.Add(toast);

      if (_toasts.Count > MaxToasts)
      {
        _toasts.RemoveAt(0);
      }
    }

    Changed?.Invoke(this, EventArgs.Empty);
    return toast;
  }

  /// <inheritdoc />
  public void Dismiss(CcToastMessage toast)
  {
    ArgumentNullException.ThrowIfNull(toast);

    bool removed;

    lock (_gate)
    {
      removed = _toasts.Remove(toast);
    }

    if (removed)
    {
      Changed?.Invoke(this, EventArgs.Empty);
    }
  }

  #endregion

  #region Events

  /// <inheritdoc />
  public event EventHandler? Changed;

  #endregion
}
