// ------------------------------------------------------------------------------------------------
// <copyright file="CircuitIdentityHandler.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server.Circuits;

using CodeCoverage.Domain.Ports;
using CodeCoverage.Infrastructure.Identity;
using CodeCoverage.Web.Configuration.Authentication;

namespace CodeCoverage.Web.Configuration.Identity;

/// <summary>Sets the user of a circuit when it opens, from the session that opened it.</summary>
/// <param name="authentication">The authentication state of the circuit, taken from the request that opened it.</param>
/// <param name="circuitUser">The principal of the circuit, which <see cref="CircuitUserScopeInitializer" /> copies.</param>
/// <param name="scopes">Creates the scope in which the user is read.</param>
/// <remarks>
///   <para>
///     The cookie names the user by its internal identifier only: the login is read from the database, in a scope of its
///     own, before the first component renders. A user that no longer exists, or keeps no token, leaves the circuit
///     anonymous. No organization and no administration right are copied (ARCHITECTURE §10bis.2).
///   </para>
///   <para>
///     The prerendering of a page runs in the request scope, whose principal <see cref="SessionCookieEvents" /> sets.
///   </para>
/// </remarks>
internal sealed class CircuitIdentityHandler(
  AuthenticationStateProvider authentication,
  CurrentUserContext circuitUser,
  IServiceScopeFactory scopes) : CircuitHandler
{
  #region Overrides of CircuitHandler

  /// <inheritdoc />
  public override async Task OnCircuitOpenedAsync(Circuit circuit, CancellationToken cancellationToken)
  {
    var state = await authentication.GetAuthenticationStateAsync().ConfigureAwait(false);

    if (circuitUser.IsAuthenticated || !SessionPrincipal.TryReadUserId(state.User, out _))
    {
      return;
    }

    var scope = scopes.CreateAsyncScope();

    await using (scope.ConfigureAwait(false))
    {
      var users = scope.ServiceProvider.GetRequiredService<IUserRepository>();

      if (await SessionUsers.FindAsync(users, state.User, cancellationToken).ConfigureAwait(false) is { } user)
      {
        circuitUser.SetUser(user.Id, user.Login, isAdmin: false);
      }
    }
  }

  #endregion
}
