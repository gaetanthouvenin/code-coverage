// ------------------------------------------------------------------------------------------------
// <copyright file="LogEntry.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.Extensions.Logging;

namespace CodeCoverage.Application.Tests.Behaviors.Support;

// A log written through a RecordingLogger: its level, rendered text, structured values and exception.
internal sealed record LogEntry(
  LogLevel Level,
  string Message,
  IReadOnlyList<KeyValuePair<string, object?>> Values,
  Exception? Exception);
