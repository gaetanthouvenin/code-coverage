// ------------------------------------------------------------------------------------------------
// <copyright file="PageState.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Web.Components.Pages;

/// <summary>Where the loading of a page stands.</summary>
public enum PageState
{
  /// <summary>The page reads what it shows.</summary>
  Loading,

  /// <summary>The page shows what it read.</summary>
  Ready,

  /// <summary>The server refused the user: the page says so.</summary>
  Denied,

  /// <summary>The reading failed: the page offers to try again.</summary>
  Failed,

  /// <summary>
  ///   What the address names is unknown or cannot be read: the page shows one state, whatever the cause (ADR-013).
  /// </summary>
  Missing,
}
