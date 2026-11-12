// ------------------------------------------------------------------------------------------------
// <copyright file="WorkerViewModel.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Web.ViewModels.Operations;

/// <summary>A worker process and its last beat.</summary>
/// <param name="Host">Its machine name and process identifier.</param>
/// <param name="LastBeatAt">When it last beat.</param>
/// <param name="IsAlive">Whether it beat within the last 2 minutes, the threshold of the suggested alert.</param>
public sealed record WorkerViewModel(string Host, DateTimeOffset LastBeatAt, bool IsAlive);
