// ------------------------------------------------------------------------------------------------
// <copyright file="ApiTokenHostTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Net;
using System.Text.Json;

using Microsoft.AspNetCore.TestHost;

using CodeCoverage.Api.Tests.Support;

namespace CodeCoverage.Api.Tests.Authentication;

// The ApiToken scheme in the host: a management endpoint answers 401 without a valid token, and runs for the user of
// the token otherwise.
[Collection(CodeCoverageApiCollection.Name)]
public sealed class ApiTokenHostTests(CodeCoverageApiFactory factory)
{
  #region Properties

  private static CancellationToken Ct => TestContext.Current.CancellationToken;

  #endregion

  #region Methods

  [Fact]
  public async Task Get_RevokedToken_ShouldReturn401WithABearerChallenge()
  {
    using var client = Client(organizationId: 9).WithApiToken(ApiTokens.RevokedValue);

    using var response = await client.GetAsync(new Uri(SecuredProbeController.Path, UriKind.Relative), Ct);

    response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    response.Headers.WwwAuthenticate.ToString().ShouldBe("Bearer");
  }

  [Theory]
  [InlineData(null)]
  [InlineData("cvc_abcdefghijklmnopqrstuvwxyz0123456789ABCDEFG")]
  public async Task Get_WithoutPersonalToken_ShouldReturn401(string? value)
  {
    using var client = Client(organizationId: 9);

    if (value is not null)
    {
      client.WithApiToken(value);
    }

    using var response = await client.GetAsync(new Uri(SecuredProbeController.Path, UriKind.Relative), Ct);

    response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
  }

  [Fact]
  public async Task Get_ValidToken_ShouldRunForItsUserInItsOrganization()
  {
    using var client = Client(organizationId: 9).WithApiToken();

    using var response = await client.GetAsync(new Uri(SecuredProbeController.Path, UriKind.Relative), Ct);
    using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));

    response.StatusCode.ShouldBe(HttpStatusCode.OK);
    body.RootElement.GetProperty("userId").GetInt64().ShouldBe(ApiTokens.UserId);
    body.RootElement.GetProperty("login").GetString().ShouldBe(ApiTokens.Login);
    body.RootElement.GetProperty("tokenOrganizationId").GetInt64().ShouldBe(9);
    body.RootElement.GetProperty("isInstanceToken").GetBoolean().ShouldBeFalse();
    body.RootElement.GetProperty("isAdmin").GetBoolean().ShouldBeFalse();
  }

  [Fact]
  public async Task Get_TokenOfTheInstance_ShouldRunWithoutOrganization()
  {
    using var client = Client(organizationId: null).WithApiToken();

    using var response = await client.GetAsync(new Uri(SecuredProbeController.Path, UriKind.Relative), Ct);
    using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));

    body.RootElement.GetProperty("isInstanceToken").GetBoolean().ShouldBeTrue();
    body.RootElement.GetProperty("tokenOrganizationId").ValueKind.ShouldBe(JsonValueKind.Null);
  }

  private HttpClient Client(long? organizationId) =>
    factory.WithWebHostBuilder(b => b.ConfigureTestServices(s => s.AddApiToken(organizationId))).CreateClient();

  #endregion
}
