// ------------------------------------------------------------------------------------------------
// <copyright file="StoredProcessingLog.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Infrastructure.Persistence.Conventions;

/// <summary>The JSON form of a processing log.</summary>
/// <param name="Messages">The messages kept.</param>
/// <param name="MessageTotal">The number of messages reported.</param>
/// <param name="Paths">The paths kept.</param>
/// <param name="PathTotal">The number of paths reported.</param>
internal sealed record StoredProcessingLog(
  IReadOnlyList<string>? Messages,
  int MessageTotal,
  IReadOnlyList<string>? Paths,
  int PathTotal);
