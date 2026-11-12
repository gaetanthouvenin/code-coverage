// ------------------------------------------------------------------------------------------------
// <copyright file="SettingEditorKind.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Web.ViewModels.Settings;

/// <summary>How a setting is edited on screen.</summary>
public enum SettingEditorKind
{
  /// <summary>A line of text, a number, an address or a list.</summary>
  Text,

  /// <summary>An on/off switch, saved as soon as it changes.</summary>
  Boolean,

  /// <summary>Several lines, e.g. YAML.</summary>
  Multiline,

  /// <summary>A secret: replaced through a dialog and never shown.</summary>
  Secret,
}
