// ------------------------------------------------------------------------------------------------
// <copyright file="CurrentUserExtensions.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Operations;
using CodeCoverage.Domain.Ports;

namespace CodeCoverage.Infrastructure.Identity;

/// <summary>Names the principal of a scope in the records it leaves (audit trail, saved settings).</summary>
internal static class CurrentUserExtensions
{
  #region Methods

  /// <summary>Names the actor of the current scope.</summary>
  /// <param name="user">The principal of the scope.</param>
  /// <returns>
  ///   <see cref="AuditEntry.SystemActor" /> for the system, the login of a signed-in user, or
  ///   <see cref="AuditEntry.AnonymousActor" />.
  /// </returns>
  /// <exception cref="ArgumentNullException"><paramref name="user" /> is <see langword="null" />.</exception>
  public static string GetActorName(this ICurrentUser user)
  {
    ArgumentNullException.ThrowIfNull(user);

    return user switch
    {
      { IsSystem: true } => AuditEntry.SystemActor,
      { IsAuthenticated: true, Login: { Length: > 0 } login } => login,
      var _ => AuditEntry.AnonymousActor,
    };
  }

  #endregion
}
