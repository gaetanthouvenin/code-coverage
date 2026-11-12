// ------------------------------------------------------------------------------------------------
// <copyright file="StatusTone.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Web.ViewModels.Commits;

/// <summary>The state of a status posted on the forge, as the pages show it.</summary>
public enum StatusTone
{
  /// <summary>The check passed.</summary>
  Success,

  /// <summary>The check failed.</summary>
  Failure,

  /// <summary>The check could not be computed.</summary>
  Error,
}
