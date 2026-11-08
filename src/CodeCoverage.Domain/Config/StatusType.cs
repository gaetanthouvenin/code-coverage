// ------------------------------------------------------------------------------------------------
// <copyright file="StatusType.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Domain.Config;

/// <summary>What a commit status measures.</summary>
public enum StatusType
{
  /// <summary>The coverage of the whole project.</summary>
  Project,

  /// <summary>The coverage of the lines of the diff.</summary>
  Patch,

  /// <summary>The coverage changes outside the diff.</summary>
  Changes,
}
