// ------------------------------------------------------------------------------------------------
// <copyright file="SettingsControllerTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

using CodeCoverage.Api.Tests.Support;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Domain.Settings;

namespace CodeCoverage.Api.Tests.Settings;

// The settings of the instance through the API: an administrator of the instance with a token of the instance.
[Collection(CodeCoverageApiCollection.Name)]
public sealed class SettingsControllerTests
{
  #region Fields

  private const string StoredSecret = "CfDJ8-protected-form-of-the-secret";

  private static readonly DateTimeOffset Now = new(2026, 11, 12, 8, 0, 0, TimeSpan.Zero);

  private readonly CodeCoverageApiFactory _factory;

  private readonly ISettingsStore _store = A.Fake<ISettingsStore>();

  private readonly IAuditLog _auditLog = A.Fake<IAuditLog>();

  #endregion

  #region Ctors

  public SettingsControllerTests(CodeCoverageApiFactory factory)
  {
    _factory = factory;

    A.CallTo(() => _store.ListAsync(A<CancellationToken>._))
     .Returns(
     [
       SettingValue.Create(Definition(SettingDefinitions.GitHubWebhookSecret), StoredSecret, SettingSource.Store, Now, "admin"),
       SettingValue.Create(Definition(SettingDefinitions.UploadsMaxFilesPerUpload), "60", SettingSource.Store, Now, "admin"),
       SettingValue.Create(Definition(SettingDefinitions.CoverageOrganizationYaml), "coverage: {}", SettingSource.Store, Now, "admin"),
     ]);
    A.CallTo(() => _store.GetAsync(A<SettingDefinition>._, A<CancellationToken>._))
     .ReturnsLazily((SettingDefinition d, CancellationToken _) => SettingValue.Create(d, d.Default, SettingSource.Default, null, null));
  }

  #endregion

  #region Properties

  private static CancellationToken Ct => TestContext.Current.CancellationToken;

  #endregion

  #region Methods

  // The acceptance of M3.12: no secret leaves, not even in its protected form.
  [Fact]
  public async Task Get_ShouldMaskSecrets()
  {
    using var client = Client(organizationId: null);

    using var response = await client.GetAsync(new Uri("/api/v1/settings", UriKind.Relative), Ct);
    var text = await response.Content.ReadAsStringAsync(Ct);
    using var body = JsonDocument.Parse(text);

    response.StatusCode.ShouldBe(HttpStatusCode.OK);
    text.ShouldNotContain(StoredSecret);
    var settings = body.RootElement.EnumerateArray().ToDictionary(static e => e.GetProperty("key").GetString()!);
    var secret = settings[SettingDefinitions.GitHubWebhookSecret];
    secret.GetProperty("isSecret").GetBoolean().ShouldBeTrue();
    secret.GetProperty("isDefined").GetBoolean().ShouldBeTrue();
    secret.GetProperty("value").ValueKind.ShouldBe(JsonValueKind.Null);
    settings[SettingDefinitions.UploadsMaxFilesPerUpload].GetProperty("value").GetString().ShouldBe("60");
    settings.ShouldNotContainKey(SettingDefinitions.CoverageOrganizationYaml);
  }

  [Fact]
  public async Task Put_EnvironmentKey_ShouldReturn422()
  {
    A.CallTo(() => _store.GetAsync(A<SettingDefinition>._, A<CancellationToken>._))
     .ReturnsLazily((SettingDefinition d, CancellationToken _) => SettingValue.Create(d, "10", SettingSource.Environment, null, null));
    using var client = Client(organizationId: null);

    using var response = await client.PutAsJsonAsync(
      new Uri($"/api/v1/settings/{SettingDefinitions.UploadsMaxFilesPerUpload}", UriKind.Relative),
      new { value = "70" },
      Ct
    );
    using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));

    response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
    body.RootElement.GetProperty("errorCode").GetString().ShouldBe("Setting.ProvidedByEnvironment");
    A.CallTo(() => _store.SaveAsync(A<SettingDefinition>._, A<string>._, A<CancellationToken>._)).MustNotHaveHappened();
  }

  [Fact]
  public async Task Put_Value_ShouldSaveAndReturn204()
  {
    using var client = Client(organizationId: null);

    using var response = await client.PutAsJsonAsync(
      new Uri($"/api/v1/settings/{SettingDefinitions.UploadsMaxFilesPerUpload}", UriKind.Relative),
      new { value = "70" },
      Ct
    );

    response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    A.CallTo(() => _store.SaveAsync(A<SettingDefinition>.That.Matches(d => d.Key == SettingDefinitions.UploadsMaxFilesPerUpload), "70", A<CancellationToken>._))
     .MustHaveHappenedOnceExactly();
  }

  [Fact]
  public async Task Put_Secret_ShouldStoreItProtectedAndNeverEchoIt()
  {
    using var client = Client(organizationId: null);

    using var response = await client.PutAsJsonAsync(
      new Uri($"/api/v1/settings/{SettingDefinitions.GitHubWebhookSecret}/secret", UriKind.Relative),
      new { value = "plain-secret-value" },
      Ct
    );

    response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    (await response.Content.ReadAsStringAsync(Ct)).ShouldNotContain("plain-secret-value");
    A.CallTo(() => _store.SaveAsync(A<SettingDefinition>._, "plain-secret-value", A<CancellationToken>._)).MustNotHaveHappened();
    A.CallTo(() => _store.SaveAsync(A<SettingDefinition>.That.Matches(d => d.Key == SettingDefinitions.GitHubWebhookSecret), A<string>._, A<CancellationToken>._))
     .MustHaveHappenedOnceExactly();
  }

  [Fact]
  public async Task Put_SecretAsPlainValue_ShouldReturn422()
  {
    using var client = Client(organizationId: null);

    using var response = await client.PutAsJsonAsync(
      new Uri($"/api/v1/settings/{SettingDefinitions.GitHubWebhookSecret}", UriKind.Relative),
      new { value = "plain" },
      Ct
    );
    using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));

    response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
    body.RootElement.GetProperty("errorCode").GetString().ShouldBe("Setting.IsSecret");
  }

  [Fact]
  public async Task Put_OrganizationKey_ShouldReturn404()
  {
    using var client = Client(organizationId: null);

    using var response = await client.PutAsJsonAsync(
      new Uri($"/api/v1/settings/{SettingDefinitions.CoverageOrganizationYaml}", UriKind.Relative),
      new { value = "coverage: {}" },
      Ct
    );

    response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
  }

  [Fact]
  public async Task Delete_ShouldResetAndReturn204()
  {
    using var client = Client(organizationId: null);

    using var response = await client.DeleteAsync(new Uri($"/api/v1/settings/{SettingDefinitions.GitHubWebhookSecretPrevious}", UriKind.Relative), Ct);

    response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    A.CallTo(() => _store.ResetAsync(A<SettingDefinition>.That.Matches(d => d.Key == SettingDefinitions.GitHubWebhookSecretPrevious), A<CancellationToken>._))
     .MustHaveHappenedOnceExactly();
  }

  // A token of an organization stays within it: the settings of the instance need a token of the instance.
  [Fact]
  public async Task Get_WithATokenOfAnOrganization_ShouldReturn403()
  {
    using var client = Client(organizationId: 3);

    using var response = await client.GetAsync(new Uri("/api/v1/settings", UriKind.Relative), Ct);

    response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
  }

  [Fact]
  public async Task Get_NotAnAdministrator_ShouldReturn403()
  {
    using var client = _factory.WithWebHostBuilder(b => b.ConfigureTestServices(s =>
                                {
                                  s.AddApiToken(organizationId: null);
                                  s.AddScoped(_ => _store);
                                }))
                               .CreateClient()
                               .WithApiToken();

    using var response = await client.GetAsync(new Uri("/api/v1/settings", UriKind.Relative), Ct);

    response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
  }

  private static SettingDefinition Definition(string key) => SettingDefinitions.Find(key)!;

  private HttpClient Client(long? organizationId) =>
    _factory.WithWebHostBuilder(b =>
             {
               // The user of the test tokens administers the instance.
               b.UseSetting("Access:AdminUserIds", ApiTokens.ExternalId);
               b.ConfigureTestServices(s =>
               {
                 s.AddApiToken(organizationId);
                 s.AddScoped(_ => _store);
                 s.AddScoped(_ => _auditLog);
               });
             })
            .CreateClient()
            .WithApiToken();

  #endregion
}
