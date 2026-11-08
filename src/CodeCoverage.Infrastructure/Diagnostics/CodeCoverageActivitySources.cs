// ------------------------------------------------------------------------------------------------
// <copyright file="CodeCoverageActivitySources.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Diagnostics;

namespace CodeCoverage.Infrastructure.Diagnostics;

/// <summary>The sources of the traces of the application, beyond those of ASP.NET Core and <c>HttpClient</c>.</summary>
/// <remarks>
///   The hosts subscribe to each name (<c>TelemetryConfiguration</c>); an activity is only created while somebody
///   listens, so the sources cost nothing when OpenTelemetry is not exported.
/// </remarks>
public static class CodeCoverageActivitySources
{
  #region Fields

  /// <summary>The name of the source of the background jobs: one activity per job run by the worker.</summary>
  public const string JobsName = "CodeCoverage.Jobs";

  /// <summary>The source of the background jobs.</summary>
  internal static readonly ActivitySource Jobs = new(JobsName);

  #endregion
}
