// ------------------------------------------------------------------------------------------------
// <copyright file="RoundingMode.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Domain.Config;

/// <summary>How a coverage value is rounded for display (<c>coverage.round</c>).</summary>
public enum RoundingMode
{
  /// <summary>Toward negative infinity.</summary>
  Down,

  /// <summary>Toward positive infinity.</summary>
  Up,

  /// <summary>To the nearest value, a tie going to the even digit.</summary>
  Nearest,
}
