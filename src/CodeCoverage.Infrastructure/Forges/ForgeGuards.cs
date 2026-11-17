// ------------------------------------------------------------------------------------------------
// <copyright file="ForgeGuards.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Diagnostics.CodeAnalysis;

using CodeCoverage.Application.Exceptions;

namespace CodeCoverage.Infrastructure.Forges;

/// <summary>
///   The refusals shared by the adapters of the forges, in the form of
///   <see cref="ArgumentNullException.ThrowIfNull(object?, string?)" />: one place holds the code and the message of
///   each.
/// </summary>
internal static class ForgeGuards
{
  #region Methods

  extension(BadGatewayException)
  {
    /// <summary>Throws when a forge connection cannot be used by the organization of the scope.</summary>
    /// <param name="unusable">Whether the connection is unknown, of another organization or suspended.</param>
    /// <param name="connectionId">The identifier of the connection.</param>
    /// <exception cref="BadGatewayException"><paramref name="unusable" /> is <see langword="true" />.</exception>
    /// <remarks>
    ///   An unknown connection and one of another organization get the same refusal: the code does not tell whether a
    ///   connection exists elsewhere.
    /// </remarks>
    public static void ThrowIfConnectionUnusable([DoesNotReturnIf(true)] bool unusable, string connectionId)
    {
      if (unusable)
      {
        throw new BadGatewayException(
          ForgeErrors.Unavailable,
          $"The forge connection '{connectionId}' cannot be used by the organization of the scope."
        );
      }
    }

    /// <summary>Throws when a forge or its identity provider refused a request or answered with an error.</summary>
    /// <param name="refused">Whether the request was refused.</param>
    /// <param name="message">The message that describes the refusal, never a secret.</param>
    /// <exception cref="BadGatewayException"><paramref name="refused" /> is <see langword="true" />.</exception>
    public static void ThrowIfForgeRefused([DoesNotReturnIf(true)] bool refused, string message)
    {
      if (refused)
      {
        throw new BadGatewayException(ForgeErrors.Unavailable, message);
      }
    }

    /// <summary>Creates the refusal of a forge that does not answer or whose answer cannot be read.</summary>
    /// <param name="message">The message that describes the failure, never a secret.</param>
    /// <param name="innerException">The exception that caused the failure.</param>
    /// <returns>The exception, for the caller to throw.</returns>
    public static BadGatewayException ForgeUnavailable(string message, Exception innerException) =>
      new(ForgeErrors.Unavailable, message, innerException);
  }

  #endregion
}
