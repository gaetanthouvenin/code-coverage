// ------------------------------------------------------------------------------------------------
// <copyright file="CcButtonVariant.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Web.Components.Ui.Models;

/// <summary>The look of a <see cref="CcButton" />.</summary>
public enum CcButtonVariant
{
  /// <summary>A soft gray button, for the ordinary actions.</summary>
  Secondary,

  /// <summary>A black (white in the dark theme) button, for the main action of a form.</summary>
  Primary,

  /// <summary>A soft red button, for a destructive action.</summary>
  Danger,

  /// <summary>A solid red button, for the confirmation of a destructive action.</summary>
  SolidDanger,

  /// <summary>A large white pill with its icon in a round, for the actions of a page header.</summary>
  Pill,

  /// <summary>A large pill in the blue gradient, for the main action of a page header.</summary>
  PillAccent,
}
