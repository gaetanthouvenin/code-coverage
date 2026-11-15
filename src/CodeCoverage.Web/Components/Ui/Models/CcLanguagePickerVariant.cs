// ------------------------------------------------------------------------------------------------
// <copyright file="CcLanguagePickerVariant.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Web.Components.Ui.Models;

/// <summary>Where a <see cref="CcLanguagePicker" /> is drawn.</summary>
public enum CcLanguagePickerVariant
{
  /// <summary>In a menu: under a rule at the bottom of the user menu, or in the panel of the language button, on two columns.</summary>
  Menu,

  /// <summary>The last line of the mobile menu, on four columns (two under 400px).</summary>
  Row,

  /// <summary>One centred line, at the bottom of the sign-in card and under the panels of the setup.</summary>
  Inline,
}
