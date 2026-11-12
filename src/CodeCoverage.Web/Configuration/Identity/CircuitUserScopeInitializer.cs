// ------------------------------------------------------------------------------------------------
// <copyright file="CircuitUserScopeInitializer.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Infrastructure.Identity;
using CodeCoverage.Web.Messaging;

namespace CodeCoverage.Web.Configuration.Identity;

/// <summary>
///   Copies the user of the circuit into the principal of each scope that
///   <see cref="ScopedSender" /> opens, so that every request runs as the user of the browser tab.
/// </summary>
/// <remarks>
///   <para>
///     Scoped to the circuit: it holds the <see cref="CurrentUserContext" /> of the circuit, set by
///     <see cref="CircuitIdentityHandler" /> when the circuit opens (or, while a page prerenders, by the
///     session cookie of the request). An anonymous circuit gives anonymous request scopes.
///   </para>
///   <para>
///     Only the identity is copied: the administration of the instance is decided for each request,
///     never taken from the circuit.
///   </para>
///   <para>
///     Only a signed-in user is copied: a circuit never runs for the system principal, which the
///     Web cannot produce (<see cref="CurrentUserContext" /> keeps it internal to the
///     Infrastructure).
///   </para>
/// </remarks>
/// <param name="circuitUser">The principal of the circuit.</param>
internal sealed class CircuitUserScopeInitializer(CurrentUserContext circuitUser) : ISendScopeInitializer
{
  #region Implementation of ISendScopeInitializer

  /// <inheritdoc />
  public void Initialize(IServiceProvider scopeServices)
  {
    ArgumentNullException.ThrowIfNull(scopeServices);

    if (circuitUser is { UserId: { } userId, Login: { } login })
    {
      scopeServices.GetRequiredService<CurrentUserContext>().SetUser(userId, login, isAdmin: false);
    }
  }

  #endregion
}
