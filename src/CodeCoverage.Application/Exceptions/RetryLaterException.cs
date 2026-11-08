// ------------------------------------------------------------------------------------------------
// <copyright file="RetryLaterException.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Application.Exceptions;

/// <summary>
///   Exception thrown by a job handler that cannot complete yet, such as a commit waiting for the
///   uploads of an ancestor, to ask the worker to run the job again after a delay.
/// </summary>
/// <remarks>
///   Internal to the job worker, which reschedules the job without consuming an attempt. It is never
///   exposed over HTTP, so it is not an <see cref="ApplicationException" />: it is not a refusal and
///   carries no error code.
/// </remarks>
public sealed class RetryLaterException : Exception
{
  #region Ctors

  /// <summary>Initializes a new instance of the <see cref="RetryLaterException" /> class.</summary>
  /// <param name="delay">The delay after which the job runs again.</param>
  /// <param name="message">The message that describes why the job must wait.</param>
  /// <exception cref="ArgumentOutOfRangeException"><paramref name="delay" /> is negative.</exception>
  public RetryLaterException(TimeSpan delay, string message)
    : base(message)
  {
    ArgumentOutOfRangeException.ThrowIfLessThan(delay, TimeSpan.Zero);

    Delay = delay;
  }

  #endregion

  #region Properties

  /// <summary>Gets the delay after which the job runs again.</summary>
  public TimeSpan Delay { get; }

  #endregion
}
