// ------------------------------------------------------------------------------------------------
// <copyright file="ProcessingLogConverter.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Text.Json;

using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

using CodeCoverage.Domain.Commits;

namespace CodeCoverage.Infrastructure.Persistence.Conventions;

/// <summary>Stores a <see cref="ProcessingLog" /> as a JSON text column.</summary>
/// <remarks>
///   The default comparer is enough: a log never changes, so a new value is always a new instance. The encoder
///   is the default one, which <see cref="ProcessingLog" /> counts at its worst to hold its cap.
/// </remarks>
internal sealed class ProcessingLogConverter : ValueConverter<ProcessingLog, string>
{
  #region Fields

  private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

  #endregion

  #region Ctors

  /// <summary>Initializes a new instance of the <see cref="ProcessingLogConverter" /> class.</summary>
  public ProcessingLogConverter()
    : base(static log => Write(log), static json => Read(json))
  {
  }

  #endregion

  #region Methods

  /// <summary>Writes a log as a JSON object.</summary>
  /// <param name="log">The log.</param>
  /// <returns>The JSON object.</returns>
  private static string Write(ProcessingLog log) =>
    JsonSerializer.Serialize(new StoredProcessingLog(log.Messages, log.MessageTotal, log.Paths, log.PathTotal), SerializerOptions);

  /// <summary>Reads a log.</summary>
  /// <param name="json">The JSON object.</param>
  /// <returns>The log; an empty one for an empty text or a JSON <c>null</c>.</returns>
  private static ProcessingLog Read(string json)
  {
    var stored = string.IsNullOrWhiteSpace(json) ? null : JsonSerializer.Deserialize<StoredProcessingLog>(json, SerializerOptions);

    return stored is null
             ? ProcessingLog.Empty
             : ProcessingLog.Restore(stored.Messages ?? [], stored.MessageTotal, stored.Paths ?? [], stored.PathTotal);
  }

  #endregion
}
