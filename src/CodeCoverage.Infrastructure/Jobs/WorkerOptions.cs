// ------------------------------------------------------------------------------------------------
// <copyright file="WorkerOptions.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Infrastructure.Jobs;

/// <summary>The <c>Worker</c> configuration section: whether the Api runs the background jobs.</summary>
/// <remarks>
///   <para>
///     A bootstrap setting, read from the environment (<c>Worker__Enabled</c>, ARCHITECTURE §4bis.2): it
///     decides at startup whether the <see cref="JobWorker" /> and the <see cref="LeaseSweeper" /> run in
///     this replica, so it is not part of the settings store catalog.
///   </para>
///   <para>
///     Only the Api registers the worker (<see cref="DependencyInjection.AddJobProcessing" />); the Web
///     never runs a job, whatever this section says.
///   </para>
/// </remarks>
public sealed class WorkerOptions
{
  #region Fields

  /// <summary>The name of the configuration section.</summary>
  public const string SectionName = "Worker";

  #endregion

  #region Properties

  /// <summary>Gets or sets a value indicating whether this replica of the Api runs the background jobs.</summary>
  /// <value><see langword="true" /> by default.</value>
  public bool Enabled { get; set; } = true;

  #endregion
}
