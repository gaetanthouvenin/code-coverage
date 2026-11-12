// ------------------------------------------------------------------------------------------------
// <copyright file="OperationsGuards.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Application.Exceptions;
using CodeCoverage.Domain.Operations;

namespace CodeCoverage.Application.Features.Operations;

/// <summary>
///   The refusals of the operations of the instance, in the form of
///   <see cref="ArgumentNullException.ThrowIfNull(object?, string?)" />: one place holds the code and the message of each.
/// </summary>
internal static class OperationsGuards
{
  #region Methods

  extension(NotFoundException)
  {
    /// <summary>Throws when the job to retry does not exist.</summary>
    /// <param name="result">What became of the retry.</param>
    /// <exception cref="NotFoundException"><paramref name="result" /> is <see cref="JobRetryResult.NotFound" />.</exception>
    public static void ThrowIfJobNotFound(JobRetryResult result)
    {
      if (result is JobRetryResult.NotFound)
      {
        throw new NotFoundException(OperationsErrors.JobNotFound, "The job does not exist.");
      }
    }
  }

  extension(ConflictException)
  {
    /// <summary>Throws when the job cannot be retried: not failed or dead, or its twin already waits.</summary>
    /// <param name="result">What became of the retry.</param>
    /// <exception cref="ConflictException"><paramref name="result" /> is <see cref="JobRetryResult.NotRetryable" />.</exception>
    public static void ThrowIfJobNotRetryable(JobRetryResult result)
    {
      if (result is JobRetryResult.NotRetryable)
      {
        throw new ConflictException(OperationsErrors.JobNotRetryable, "The job is not failed or dead, or its twin already waits.");
      }
    }
  }

  #endregion
}
