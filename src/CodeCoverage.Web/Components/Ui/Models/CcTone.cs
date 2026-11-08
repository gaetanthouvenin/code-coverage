// ------------------------------------------------------------------------------------------------
// <copyright file="CcTone.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Web.Components.Ui.Models;

/// <summary>The semantic colour of a badge or a banner.</summary>
public enum CcTone
{
  /// <summary>A neutral, informative-only value.</summary>
  Neutral,

  /// <summary>An information.</summary>
  Info,

  /// <summary>A success, or a coverage above its target.</summary>
  Success,

  /// <summary>A warning, or a partial coverage.</summary>
  Warning,

  /// <summary>An error, a failure or a missed coverage.</summary>
  Danger,
}
