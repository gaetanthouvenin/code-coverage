// ------------------------------------------------------------------------------------------------
// <copyright file="ConnectionGuards.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Diagnostics.CodeAnalysis;

using CodeCoverage.Application.Exceptions;
using CodeCoverage.Domain.Organizations;

namespace CodeCoverage.Application.Features.Connections;

/// <summary>
///   The refusals of the connections, in the form of
///   <see cref="ArgumentNullException.ThrowIfNull(object?, string?)" />: one place holds the code and the message of each.
/// </summary>
internal static class ConnectionGuards
{
  #region Methods

  extension(UnprocessableEntityException)
  {
    /// <summary>Throws when the forge refuses a connection access to the account it names.</summary>
    /// <param name="isRefused">Whether the forge refuses the connection.</param>
    /// <param name="refusal">Why the forge refuses it, never a secret, or <see langword="null" />.</param>
    /// <param name="forge">The forge of the account.</param>
    /// <param name="login">The login of the account.</param>
    /// <exception cref="UnprocessableEntityException"><paramref name="isRefused" /> is <see langword="true" />.</exception>
    public static void ThrowIfForgeAccessDenied([DoesNotReturnIf(true)] bool isRefused, string? refusal, Forge forge, string login)
    {
      if (isRefused)
      {
        throw new UnprocessableEntityException(ConnectionErrors.ForgeAccessDenied, $"{forge} refuses the connection access to '{login}': {refusal}");
      }
    }

    /// <summary>Throws when neither an installation nor a connection serves the organization.</summary>
    /// <typeparam name="T">The type of what serves it: an installation, a connection or the job that reads it again.</typeparam>
    /// <param name="served">What serves the organization.</param>
    /// <param name="forge">The forge of the organization.</param>
    /// <exception cref="ArgumentNullException"><paramref name="served" /> is <see langword="null" />.</exception>
    /// <exception cref="UnprocessableEntityException"><paramref name="served" /> is empty.</exception>
    public static void ThrowIfNotConnected<T>(IReadOnlyCollection<T> served, Forge forge)
    {
      ArgumentNullException.ThrowIfNull(served);

      if (served.Count == 0)
      {
        throw new UnprocessableEntityException(ConnectionErrors.NotConnected, $"No installation nor connection serves this {forge} organization.");
      }
    }
  }

  #endregion
}
