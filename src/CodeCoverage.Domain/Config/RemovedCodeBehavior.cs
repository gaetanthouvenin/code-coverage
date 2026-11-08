// ------------------------------------------------------------------------------------------------
// <copyright file="RemovedCodeBehavior.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Domain.Config;

/// <summary>How a failing <c>project</c> status can still pass (<c>removed_code_behavior</c>).</summary>
public enum RemovedCodeBehavior
{
  /// <summary>The base is recomputed without the lines the diff removes.</summary>
  AdjustBase,

  /// <summary>It passes if the diff only removes lines and changes no other coverage.</summary>
  RemovalsOnly,

  /// <summary>It passes if the patch is fully covered and no other coverage changes.</summary>
  FullyCoveredPatch,

  /// <summary>A failing status stays failing.</summary>
  Off,
}
