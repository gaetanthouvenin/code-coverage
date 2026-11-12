// ------------------------------------------------------------------------------------------------
// <copyright file="WorkerHeartbeat.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Domain.Operations;

/// <summary>The last beat of a worker process, persisted so that any process can read it.</summary>
/// <remarks>Operational data: no organization, no business data.</remarks>
public sealed class WorkerHeartbeat
{
  #region Fields

  /// <summary>The longest name of a host: the machine name and the identifier of the process.</summary>
  public const int HostMaxLength = 128;

  #endregion

  #region Ctors

  private WorkerHeartbeat(string host, DateTimeOffset startedAt, DateTimeOffset lastBeatAt)
  {
    Host = host;
    StartedAt = startedAt;
    LastBeatAt = lastBeatAt;
  }

  #endregion

  #region Properties

  /// <summary>Gets the worker: its machine name and the identifier of its process.</summary>
  public string Host { get; private set; }

  /// <summary>Gets when the process started.</summary>
  public DateTimeOffset StartedAt { get; private set; }

  /// <summary>Gets when the worker last beat.</summary>
  public DateTimeOffset LastBeatAt { get; private set; }

  #endregion

  #region Methods

  /// <summary>Creates the heartbeat of a worker.</summary>
  /// <param name="host">The worker: its machine name and the identifier of its process.</param>
  /// <param name="startedAt">When the process started; converted to UTC.</param>
  /// <param name="lastBeatAt">When the worker last beat; converted to UTC.</param>
  /// <returns>The heartbeat.</returns>
  /// <exception cref="ArgumentException"><paramref name="host" /> is empty or longer than <see cref="HostMaxLength" />.</exception>
  public static WorkerHeartbeat Create(string host, DateTimeOffset startedAt, DateTimeOffset lastBeatAt)
  {
    ArgumentException.ThrowIfNullOrWhiteSpace(host);
    ArgumentOutOfRangeException.ThrowIfGreaterThan(host.Length, HostMaxLength, nameof(host));

    return new WorkerHeartbeat(host, startedAt.ToUniversalTime(), lastBeatAt.ToUniversalTime());
  }

  #endregion
}
