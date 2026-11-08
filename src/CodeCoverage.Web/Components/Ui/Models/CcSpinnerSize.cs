// ------------------------------------------------------------------------------------------------
// <copyright file="CcSpinnerSize.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Web.Components.Ui.Models;

/// <summary>The size of a <see cref="CcLoadingSpinner" />.</summary>
public enum CcSpinnerSize
{
  /// <summary>A small ring, inline in a button or a line of text.</summary>
  Small,

  /// <summary>The default ring, inline with its label.</summary>
  Medium,

  /// <summary>A large ring above its label, in place of the content of a card or a page.</summary>
  Large,
}
