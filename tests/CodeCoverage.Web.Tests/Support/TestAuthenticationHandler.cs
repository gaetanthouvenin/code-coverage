// ------------------------------------------------------------------------------------------------
// <copyright file="TestAuthenticationHandler.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Globalization;
using System.Text.Encodings.Web;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using CodeCoverage.Domain.Organizations;
using CodeCoverage.Infrastructure.Identity;
using CodeCoverage.Web.Configuration.Authentication;

namespace CodeCoverage.Web.Tests.Support;

// Stands for the session cookie in the host tests: a request that carries the header runs for that user, as
// SessionCookieEvents would set it; any other request goes to the cookie scheme, as in production.
public sealed class TestAuthenticationHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
  : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
  #region Fields

  public const string SchemeName = "Test";

  public const string UserHeader = "X-Test-User";

  public const string Login = "test-user";

  #endregion

  #region Methods

  protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
  {
    if (!long.TryParse(Request.Headers[UserHeader], NumberStyles.None, CultureInfo.InvariantCulture, out var userId))
    {
      return await Context.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    }

    Context.RequestServices.GetRequiredService<CurrentUserContext>().SetUser(userId, Login, Forge.GitHub, "583231");

    return AuthenticateResult.Success(new AuthenticationTicket(SessionPrincipal.Create(userId, DateTimeOffset.UnixEpoch), SchemeName));
  }

  #endregion
}
