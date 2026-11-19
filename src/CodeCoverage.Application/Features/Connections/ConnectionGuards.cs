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
  }

  #endregion
}
