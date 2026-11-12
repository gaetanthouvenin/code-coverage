// ------------------------------------------------------------------------------------------------
// <copyright file="HistoryInterval.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Domain.History;

/// <summary>How the points of a coverage history are grouped.</summary>
public enum HistoryInterval
{
  /// <summary>One point per commit.</summary>
  Commit,

  /// <summary>The last value of each day, in UTC.</summary>
  Day,

  /// <summary>The last value of each ISO week, from Monday, in UTC.</summary>
  Week,
}
