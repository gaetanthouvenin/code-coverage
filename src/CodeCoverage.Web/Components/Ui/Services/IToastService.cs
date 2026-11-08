// ------------------------------------------------------------------------------------------------
// <copyright file="IToastService.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Web.Components.Ui.Models;

namespace CodeCoverage.Web.Components.Ui.Services;

/// <summary>
///   Raises the toasts of a circuit: short messages shown at the bottom right of the screen by the
///   <see cref="CcToast" /> stack, then dismissed after a few seconds or by the user.
/// </summary>
/// <remarks>
///   Scoped: each circuit (browser tab) has its own toasts. The service may be called from any
///   thread; the stack renders the change on the dispatcher of its circuit.
/// </remarks>
public interface IToastService
{
  #region Properties

  /// <summary>Gets a snapshot of the toasts shown, the oldest first.</summary>
  IReadOnlyList<CcToastMessage> Toasts { get; }

  #endregion

  #region Methods

  /// <summary>Shows a toast.</summary>
  /// <param name="tone">The semantic colour of the toast.</param>
  /// <param name="title">The localized bold first line.</param>
  /// <param name="message">The localized second line, if any.</param>
  /// <returns>The toast shown, to dismiss it early if needed.</returns>
  /// <exception cref="ArgumentException"><paramref name="title" /> is <see langword="null" />, empty or white space.</exception>
  CcToastMessage Show(CcTone tone, string title, string? message = null);

  /// <summary>Dismisses a toast; does nothing when it is no longer shown.</summary>
  /// <param name="toast">The toast to dismiss.</param>
  /// <exception cref="ArgumentNullException"><paramref name="toast" /> is <see langword="null" />.</exception>
  void Dismiss(CcToastMessage toast);

  #endregion

  #region Events

  /// <summary>Occurs when a toast is shown or dismissed.</summary>
  event EventHandler? Changed;

  #endregion
}
