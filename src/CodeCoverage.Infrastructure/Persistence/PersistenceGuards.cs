// ------------------------------------------------------------------------------------------------
// <copyright file="PersistenceGuards.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Application.Exceptions;
using CodeCoverage.Application.Exceptions.ErrorCodes;

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
  }

  #endregion
}
