// ------------------------------------------------------------------------------------------------
// <copyright file="ApiTokens.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;

using Microsoft.Extensions.DependencyInjection;

using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Domain.Tokens;
using CodeCoverage.Domain.Users;

namespace CodeCoverage.Api.Tests.Support;

// The personal tokens of the API (cvp_) of the host tests, whose database is unreachable: the repositories of the
// tokens and of the users are faked, the rest of the authentication runs as in production.
public static class ApiTokens
{
  #region Fields

  public const string Value = "cvp_abcdefghijklmnopqrstuvwxyz0123456789ABCDEFG";

  public const string RevokedValue = "cvp_revokedrevokedrevokedrevokedrevokedrevok";

  public const long UserId = 42;

  public const string Login = "octocat";

  public const string ExternalId = "583231";

  #endregion

  #region Methods

  // Registers the token Value, bound to an organization or to the instance (null), and RevokedValue, revoked.
  public static IServiceCollection AddApiToken(this IServiceCollection services, long? organizationId)
  {
    var now = DateTimeOffset.UtcNow;
    var tokens = A.Fake<IApiTokenRepository>();
    var users = A.Fake<IUserRepository>();
    var user = AppUser.Create(Forge.GitHub, ExternalId, Login, null);
    var token = ApiToken.Create(UserId, organizationId, "tests", Value[..8], Hash(Value), now.AddDays(-1), null);
    var revoked = ApiToken.Create(UserId, organizationId, "revoked", RevokedValue[..8], Hash(RevokedValue), now.AddDays(-1), null);

    revoked.Revoke(now.AddHours(-1));
    typeof(AppUser).GetProperty(nameof(AppUser.Id))!.SetValue(user, UserId);
    A.CallTo(() => tokens.FindByHashAsync(A<byte[]>.That.IsSameSequenceAs(Hash(Value)), A<CancellationToken>._)).Returns(token);
    A.CallTo(() => tokens.FindByHashAsync(A<byte[]>.That.IsSameSequenceAs(Hash(RevokedValue)), A<CancellationToken>._)).Returns(revoked);
    A.CallTo(() => users.FindAsync(UserId, A<CancellationToken>._)).Returns(user);

    services.AddScoped(_ => tokens);
    services.AddScoped(_ => users);

    return services;
  }

  public static HttpClient WithApiToken(this HttpClient client, string value = Value)
  {
    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", value);

    return client;
  }

  private static byte[] Hash(string value) => SHA256.HashData(Encoding.UTF8.GetBytes(value));

  #endregion
}
