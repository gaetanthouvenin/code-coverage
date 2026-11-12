// ------------------------------------------------------------------------------------------------
// <copyright file="OperationsErrors.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Application.Features.Operations;

/// <summary>The error codes of the operations of the instance.</summary>
public static class OperationsErrors
{
  #region Fields

  /// <summary>No job has this identifier, or it was purged. Maps to HTTP 404 Not Found.</summary>
  public const string JobNotFound = "Operations.JobNotFound";

  /// <summary>
  ///   The job is not failed or dead, or a twin with the same deduplication key already waits. Maps to HTTP 409
  ///   Conflict.
  /// </summary>
  public const string JobNotRetryable = "Operations.JobNotRetryable";

  /// <summary>A page, a page size or a count is out of its bounds. Maps to HTTP 400 Bad Request.</summary>
  public const string InvalidPage = "Operations.InvalidPage";

  #endregion
}
