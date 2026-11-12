// ------------------------------------------------------------------------------------------------
// <copyright file="OrganizationsControllerTests.cs" company="Gaëtan Thouvenin">
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
using CodeCoverage.Domain.Settings;

namespace CodeCoverage.Api.Tests.Organizations;

// The settings of an organization through the API: an administrator of the organization only, and only the
// organization of the token.
[Collection(CodeCoverageApiCollection.Name)]
public sealed class OrganizationsControllerTests
{
  #region Fields

  private const string Settings = "/api/v1/orgs/github/octo-org/settings";

  private readonly CodeCoverageApiFactory _factory;

  private readonly IOrganizationScope _scope = A.Fake<IOrganizationScope>();

  private readonly ICurrentOrganization _organization = A.Fake<ICurrentOrganization>();

  private readonly IOrganizationRoleResolver _roles = A.Fake<IOrganizationRoleResolver>();

  private readonly IOrganizationSettingRepository _settings = A.Fake<IOrganizationSettingRepository>();

  private readonly IAuditLog _auditLog = A.Fake<IAuditLog>();

  #endregion

  #region Ctors

  public OrganizationsControllerTests(CodeCoverageApiFactory factory)
  {
    _factory = factory;

    A.CallTo(() => _scope.TryEnterAsync(Forge.GitHub, "octo-org", A<CancellationToken>._)).Returns(true);
    A.CallTo(() => _organization.Id).Returns(3L);
    A.CallTo(() => _roles.ResolveAsync(ApiTokens.UserId, 3, A<CancellationToken>._)).Returns(OrganizationRole.Admin);
    A.CallTo(() => _settings.ListAsync(A<CancellationToken>._)).Returns([]);
  }

  #endregion

  #region Properties

  private static CancellationToken Ct => TestContext.Current.CancellationToken;

  #endregion

  #region Methods

  [Fact]
  public async Task Get_Admin_ShouldReturnTheFourKeys()
  {
    using var client = Client();

    using var response = await client.GetAsync(new Uri(Settings, UriKind.Relative), Ct);
    using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));

    response.StatusCode.ShouldBe(HttpStatusCode.OK);
    body.RootElement.GetArrayLength().ShouldBe(4);
    body.RootElement.EnumerateArray().ShouldAllBe(e => e.GetProperty("source").GetString() == "Default");
  }

  [Fact]
  public async Task Get_Participant_ShouldReturn403()
  {
    Participant();
    using var client = Client();

    using var response = await client.GetAsync(new Uri(Settings, UriKind.Relative), Ct);

    response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
  }

  [Fact]
  public async Task Put_Participant_ShouldReturn403()
  {
    Participant();
    using var client = Client();

    using var response = await client.PutAsJsonAsync(new Uri($"{Settings}/{SettingDefinitions.NotificationsStatusContextPrefix}", UriKind.Relative), new { value = "codecov" }, Ct);

    response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    A.CallTo(_settings).MustNotHaveHappened();
  }

  // The gate enters no other organization than the one of the token: another one is unknown, never forbidden.
  [Fact]
  public async Task Put_OtherOrganization_ShouldReturn404()
  {
    using var client = Client();

    using var response = await client.PutAsJsonAsync(new Uri($"/api/v1/orgs/github/other-org/settings/{SettingDefinitions.NotificationsStatusContextPrefix}", UriKind.Relative), new { value = "codecov" }, Ct);

    response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    A.CallTo(_settings).MustNotHaveHappened();
  }

  [Fact]
  public async Task Put_Admin_ShouldReturn204()
  {
    using var client = Client();

    using var response = await client.PutAsJsonAsync(new Uri($"{Settings}/{SettingDefinitions.NotificationsStatusContextPrefix}", UriKind.Relative), new { value = "codecov" }, Ct);

    response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    A.CallTo(() => _settings.SaveAsync(A<SettingDefinition>._, "codecov", A<string>._, A<DateTimeOffset>._, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
  }

  [Fact]
  public async Task Put_InstanceKey_ShouldReturn404()
  {
    using var client = Client();

    using var response = await client.PutAsJsonAsync(new Uri($"{Settings}/{SettingDefinitions.AccessAdminUserIds}", UriKind.Relative), new { value = "1" }, Ct);

    response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
  }

  [Fact]
  public async Task Delete_Admin_ShouldReturn204()
  {
    using var client = Client();

    using var response = await client.DeleteAsync(new Uri($"{Settings}/{SettingDefinitions.CoverageOrganizationYaml}", UriKind.Relative), Ct);

    response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
  }

  private void Participant()
  {
    A.CallTo(() => _roles.ResolveAsync(A<long>._, A<long>._, A<CancellationToken>._)).Returns(OrganizationRole.Participant);
  }

  private HttpClient Client() =>
    _factory.WithWebHostBuilder(b => b.ConfigureTestServices(s =>
                                {
                                  s.AddApiToken(organizationId: 3);
                                  s.AddScoped(_ => _scope);
                                  s.AddScoped(_ => _organization);
                                  s.AddScoped(_ => _roles);
                                  s.AddScoped(_ => _settings);
                                  s.AddScoped(_ => _auditLog);
                                }))
            .CreateClient()
            .WithApiToken();

  #endregion
}
