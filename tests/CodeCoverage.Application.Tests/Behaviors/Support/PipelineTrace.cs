// ------------------------------------------------------------------------------------------------
// <copyright file="PipelineTrace.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Application.Tests.Behaviors.Support;

// Shared, ordered record of what the pipeline did: logs, validations and the handler.
internal sealed class PipelineTrace
{
  #region Properties

  public List<LogEntry> Logs { get; } = [];

  public List<string> Steps { get; } = [];

  #endregion
}
