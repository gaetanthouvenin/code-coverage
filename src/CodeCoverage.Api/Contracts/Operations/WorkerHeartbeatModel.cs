// ------------------------------------------------------------------------------------------------
// <copyright file="WorkerHeartbeatModel.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Api.Contracts.Operations;

/// <summary>The last beat of a worker process.</summary>
/// <param name="Host">The worker: its machine name and the identifier of its process.</param>
/// <param name="StartedAt">When the process started.</param>
/// <param name="LastBeatAt">When the worker last beat.</param>
public sealed record WorkerHeartbeatModel(string Host, DateTimeOffset StartedAt, DateTimeOffset LastBeatAt);
