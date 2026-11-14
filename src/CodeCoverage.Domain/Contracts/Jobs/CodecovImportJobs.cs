// ------------------------------------------------------------------------------------------------
// <copyright file="CodecovImportJobs.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Globalization;
using System.Text.Json;

namespace CodeCoverage.Domain.Contracts.Jobs;

/// <summary>Builds the import of the history of Codecov, queued with its <c>CodecovImports</c> row.</summary>
public static class CodecovImportJobs
{
  #region Methods

  /// <summary>Builds the job, in the organization of the scope.</summary>
  /// <param name="importId">The identifier of the import; the payload holds nothing else, never the token.</param>
  /// <param name="repositoryId">The identifier of the repository: the imports of one repository never run together.</param>
  /// <returns>The job.</returns>
  public static JobRequest Create(long importId, long repositoryId) =>
    JobRequest.Create(
      JobTypes.ImportCodecovHistory,
      JsonSerializer.Serialize(new { ImportId = importId }),
      string.Create(CultureInfo.InvariantCulture, $"codecov-import:{repositoryId}"),
      string.Create(CultureInfo.InvariantCulture, $"codecov-import:{importId}")
    );

  /// <summary>Reads the identifier of the import from the payload of its job.</summary>
  /// <param name="payloadJson">The payload of the job.</param>
  /// <returns>The identifier of the import.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="payloadJson" /> is <see langword="null" />.</exception>
  /// <exception cref="JsonException">The payload is not JSON.</exception>
  /// <exception cref="KeyNotFoundException">The payload has no identifier.</exception>
  /// <exception cref="InvalidOperationException">The identifier is not a number.</exception>
  public static long ReadImportId(string payloadJson)
  {
    ArgumentNullException.ThrowIfNull(payloadJson);

    using var payload = JsonDocument.Parse(payloadJson);

    return payload.RootElement.GetProperty("ImportId").GetInt64();
  }

  #endregion
}
