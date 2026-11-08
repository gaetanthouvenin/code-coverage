// ------------------------------------------------------------------------------------------------
// <copyright file="CcToastMessage.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Web.Components.Ui.Models;

/// <summary>A toast shown by the <see cref="CcToast" /> stack, raised through <see cref="Services.IToastService" />.</summary>
/// <param name="Tone">
///   The semantic colour: <see cref="CcTone.Danger" /> for a failure, <see cref="CcTone.Success" />
///   for a confirmation.
/// </param>
/// <param name="Title">The localized bold first line.</param>
/// <param name="Message">The localized second line, if any.</param>
public sealed record CcToastMessage(CcTone Tone, string Title, string? Message = null)
{
  #region Properties

  /// <summary>Gets the identity of the toast, which tells two toasts with the same text apart.</summary>
  public Guid Id { get; } = Guid.NewGuid();

  #endregion
}
