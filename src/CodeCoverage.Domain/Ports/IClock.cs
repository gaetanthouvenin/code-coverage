// ------------------------------------------------------------------------------------------------
// <copyright file="IClock.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Domain.Ports;

/// <summary>The current time, replaceable in tests.</summary>
public interface IClock
{
  #region Properties

  /// <summary>Gets the current UTC time.</summary>
  DateTimeOffset UtcNow { get; }

  #endregion
}
