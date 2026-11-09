// ------------------------------------------------------------------------------------------------
// <copyright file="SessionKind.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Domain.Commits;

/// <summary>Where the coverage of a session comes from.</summary>
public enum SessionKind
{
  /// <summary>An upload received for the commit.</summary>
  Uploaded,

  /// <summary>A session copied from an ancestor, for a flag that was not uploaded.</summary>
  CarriedForward,
}
