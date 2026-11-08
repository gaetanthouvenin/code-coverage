// ------------------------------------------------------------------------------------------------
// <copyright file="InvalidJobException.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Infrastructure.Jobs;

/// <summary>
///   Exception thrown by the <see cref="JobDispatcher" /> for a job it cannot turn into a command: unknown
///   type, or payload that is not the JSON of its type.
/// </summary>
/// <remarks>
///   Running the job again would fail the same way: the <see cref="JobWorker" /> marks it failed for good,
///   without retry.
/// </remarks>
internal sealed class InvalidJobException : Exception
{
  #region Ctors

  /// <summary>Initializes a new instance of the <see cref="InvalidJobException" /> class.</summary>
  /// <param name="message">The message that describes why the job cannot run.</param>
  public InvalidJobException(string message)
    : base(message)
  {
  }

  /// <summary>Initializes a new instance of the <see cref="InvalidJobException" /> class with its cause.</summary>
  /// <param name="message">The message that describes why the job cannot run.</param>
  /// <param name="innerException">The error that made the job invalid.</param>
  public InvalidJobException(string message, Exception innerException)
    : base(message, innerException)
  {
  }

  #endregion
}
