// ------------------------------------------------------------------------------------------------
// <copyright file="ApiTokensControllerTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

using CodeCoverage.Api.Tests.Support;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Ports;

namespace CodeCoverage.Api.Tests.Tokens;

// The personal API tokens through the API, authenticated by a token of an organization.
[Collection(CodeCoverageApiCollection.Name)]
public sealed class ApiTokensControllerTests
{
  #region Fields

  private const string Path = "/api/v1/api-tokens";

  private static readonly DateTimeOffset Now = new(2026, 11, 12, 8, 0, 0, TimeSpan.Zero);

  private readonly CodeCoverageApiFactory _factory;

  private readonly IOrganizationDirectory _directory = A.Fake<IOrganizationDirectory>();

  private readonly IOrganizationRoleResolver _roles = A.Fake<IOrganizationRoleResolver>();

  private readonly IAuditLog _auditLog = A.Fake<IAuditLog>();

  #endregion

  #region Ctors

  public ApiTokensControllerTests(CodeCoverageApiFactory factory)
  {
    _factory = factory;

    var organization = Organization.CreatePending(Forge.GitHub, "1001", "octo-org", OrganizationAccountType.Organization, Now);
    organization.Admit("admin", Now);
    typeof(Organization).GetProperty(nameof(Organization.Id))!.SetValue(organization, 3L);

    A.CallTo(() => _directory.FindByLoginAsync(Forge.GitHub, "octo-org", A<CancellationToken>._)).Returns(organization);
    A.CallTo(() => _roles.ResolveAsync(ApiTokens.UserId, 3, A<CancellationToken>._)).Returns(OrganizationRole.Participant);
  }

  #endregion

  #region Properties

  private static CancellationToken Ct => TestContext.Current.CancellationToken;

  #endregion

  #region Methods

  [Fact]
  public async Task Post_TokenOfTheOrganization_ShouldReturnItsValueOnce()
  {
    using var client = Client();

    using var response = await client.PostAsJsonAsync(new Uri(Path, UriKind.Relative), new { name = "ci", forge = "github", organization = "octo-org" }, Ct);
    using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));

    response.StatusCode.ShouldBe(HttpStatusCode.Created);
    body.RootElement.GetProperty("value").GetString().ShouldStartWith("cvp_");
    body.RootElement.GetProperty("isInstanceToken").GetBoolean().ShouldBeFalse();
  }

  // A token of an organization cannot create a token of the instance.
  [Fact]
  public async Task Post_TokenOfTheInstance_FromATokenOfAnOrganization_ShouldReturn403()
  {
    using var client = Client();

    using var response = await client.PostAsJsonAsync(new Uri(Path, UriKind.Relative), new { name = "ops" }, Ct);

    response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
  }

  [Fact]
  public async Task Post_UnknownForge_ShouldReturn400()
  {
    using var client = Client();

    using var response = await client.PostAsJsonAsync(new Uri(Path, UriKind.Relative), new { name = "ci", forge = "nowhere", organization = "octo-org" }, Ct);

    response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
  }

  [Fact]
  public async Task Get_ShouldNeverReturnValue()
  {
    using var client = Client();

    using var response = await client.GetAsync(new Uri(Path, UriKind.Relative), Ct);
    var json = await response.Content.ReadAsStringAsync(Ct);

    response.StatusCode.ShouldBe(HttpStatusCode.OK);
    json.ShouldNotContain(ApiTokens.Value[8..]);
  }

  [Fact]
  public async Task Delete_UnknownToken_ShouldReturn404()
  {
    using var client = Client();

    using var response = await client.DeleteAsync(new Uri($"{Path}/99", UriKind.Relative), Ct);

    response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
  }

  private HttpClient Client() =>
    _factory.WithWebHostBuilder(b => b.ConfigureTestServices(s =>
                                {
                                  s.AddApiToken(organizationId: 3);
                                  s.AddScoped(_ => _directory);
                                  s.AddScoped(_ => _roles);
                                  s.AddScoped(_ => _auditLog);
                                }))
            .CreateClient()
            .WithApiToken();

  #endregion
}
