// ------------------------------------------------------------------------------------------------
// <copyright file="PersistenceGuards.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Diagnostics.CodeAnalysis;

using CodeCoverage.Application.Exceptions;
using CodeCoverage.Application.Exceptions.ErrorCodes;
using CodeCoverage.Application.Features.Commits;
using CodeCoverage.Application.Features.Uploads;

namespace CodeCoverage.Infrastructure.Persistence;

/// <summary>
///   The refusals of the repositories, in the form of
///   <see cref="ArgumentNullException.ThrowIfNull(object?, string?)" />: one place holds the code of each.
/// </summary>
internal static class PersistenceGuards
{
  #region Methods

  extension(ConflictException)
  {
    /// <summary>Creates the refusal of a save that met a row written in the meantime by another save.</summary>
    /// <param name="message">The message that describes the conflict.</param>
    /// <param name="innerException">The failure of the save.</param>
    /// <returns>The exception, for the caller to throw.</returns>
    /// <remarks>The error code is <see cref="GeneralErrors.ConcurrentChange" />.</remarks>
    public static ConflictException ConcurrentChange(string message, Exception innerException) =>
      new(GeneralErrors.ConcurrentChange, message, innerException);

    /// <summary>Throws when a row the save depends on was changed or removed in the meantime.</summary>
    /// <param name="changed">Whether the row is gone or was not updated.</param>
    /// <param name="message">The message that describes the conflict.</param>
    /// <exception cref="ConflictException"><paramref name="changed" /> is <see langword="true" />.</exception>
    /// <remarks>The error code is <see cref="GeneralErrors.ConcurrentChange" />.</remarks>
    public static void ThrowIfConcurrentChange([DoesNotReturnIf(true)] bool changed, string message)
    {
      if (changed)
      {
        throw new ConflictException(GeneralErrors.ConcurrentChange, message);
      }
    }

    /// <summary>Creates the refusal of the finalization of a commit that another finalization saved in the meantime.</summary>
    /// <param name="message">The message that describes the conflict.</param>
    /// <param name="innerException">The failure of the save.</param>
    /// <returns>The exception, for the caller to throw.</returns>
    /// <remarks>The error code is <see cref="CommitErrors.ConcurrentSave" />.</remarks>
    public static ConflictException CommitConcurrentSave(string message, Exception innerException) =>
      new(CommitErrors.ConcurrentSave, message, innerException);

    /// <summary>Creates the refusal of an upload whose idempotency key is already used on its target.</summary>
    /// <param name="message">The message that describes the conflict.</param>
    /// <param name="innerException">The failure of the save.</param>
    /// <returns>The exception, for the caller to throw.</returns>
    /// <remarks>The error code is <see cref="UploadErrors.Duplicate" />.</remarks>
    public static ConflictException UploadDuplicate(string message, Exception innerException) =>
      new(UploadErrors.Duplicate, message, innerException);
  }

  #endregion
}
