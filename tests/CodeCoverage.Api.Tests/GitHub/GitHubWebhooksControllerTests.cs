// ------------------------------------------------------------------------------------------------
// <copyright file="GitHubWebhooksControllerTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Net;
using System.Text;
using System.Text.Json;

using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

using CodeCoverage.Api.Tests.Support;
using CodeCoverage.Domain.Contracts.GitHub;
using CodeCoverage.Domain.Contracts.Jobs;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Domain.Repositories;

namespace CodeCoverage.Api.Tests.GitHub;

[Collection(CodeCoverageApiCollection.Name)]
public sealed class GitHubWebhooksControllerTests(CodeCoverageApiFactory factory)
{
  #region Fields

  private const string WebhooksPath = CodeCoverageApi.Prefix + "/github/webhooks";

  private const string Body = """{"installation":{"id":42},"repository":{"id":7}}""";

  private readonly IWebhookSignatureValidator _signatures = A.Fake<IWebhookSignatureValidator>();

  private readonly IInstallationRepository _installations = A.Fake<IInstallationRepository>();

  private readonly IWebhookDeliveryRepository _deliveries = A.Fake<IWebhookDeliveryRepository>();

  private readonly IForgeGatewayProvider _forges = A.Fake<IForgeGatewayProvider>();

  #endregion

  #region Properties

  private static CancellationToken Ct => TestContext.Current.CancellationToken;

  #endregion

  #region Methods

  [Fact]
  public async Task Post_Valid_ShouldReturn202WithoutCallingGitHub()
  {
    ArrangeAccepted(stored: true);
    await using var host = Host();
    using var client = host.CreateClient();

    using var response = await PostAsync(client, "text/plain");

    response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
    A.CallTo(_forges).MustNotHaveHappened();
  }

  // The signature covers the bytes GitHub sent: nothing may have read or reformatted them.
  [Fact]
  public async Task Post_ShouldGiveTheRawBodyAndTheHeadersToTheValidator()
  {
    ArrangeAccepted(stored: true);
    await using var host = Host();
    using var client = host.CreateClient();

    using var response = await PostAsync(client, "application/x-www-form-urlencoded");

    response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
    A.CallTo(() => _signatures.IsValid(A<byte[]>.That.Matches(static b => Encoding.UTF8.GetString(b) == Body), "sha256=abc"))
     .MustHaveHappenedOnceExactly();
  }

  [Fact]
  public async Task Post_KnownDelivery_ShouldReturn200()
  {
    ArrangeAccepted(stored: false);
    await using var host = Host();
    using var client = host.CreateClient();

    using var response = await PostAsync(client, "application/json");

    response.StatusCode.ShouldBe(HttpStatusCode.OK);
  }

  [Fact]
  public async Task Post_InvalidSignature_ShouldReturn401()
  {
    A.CallTo(() => _signatures.IsValid(A<byte[]>._, A<string?>._)).Returns(false);
    await using var host = Host();
    using var client = host.CreateClient();

    using var response = await PostAsync(client, "application/json");
    using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));

    response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    problem.RootElement.GetProperty("errorCode").GetString().ShouldBe("GitHubEvent.InvalidSignature");
    A.CallTo(_deliveries).MustNotHaveHappened();
  }

  private void ArrangeAccepted(bool stored)
  {
    A.CallTo(() => _signatures.IsValid(A<byte[]>._, A<string?>._)).Returns(true);
    A.CallTo(() => _installations.FindOwnerAsync(42, A<CancellationToken>._)).Returns(new InstallationOwner(9, OrganizationState.Active));
    A.CallTo(() => _deliveries.TryStoreAsync(A<GitHubWebhookEvent>._, A<JobRequest>._, A<DateTimeOffset>._, A<CancellationToken>._)).Returns(stored);
  }

  private Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> Host() =>
    factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
    {
      services.AddSingleton(_signatures);
      services.AddSingleton(_installations);
      services.AddSingleton(_deliveries);
      services.AddSingleton(_forges);
    }));

  private static async Task<HttpResponseMessage> PostAsync(HttpClient client, string contentType)
  {
    using var request = new HttpRequestMessage(HttpMethod.Post, WebhooksPath)
    {
      Content = new StringContent(Body, Encoding.UTF8, contentType),
    };

    request.Headers.Add("X-GitHub-Delivery", "d-1");
    request.Headers.Add("X-GitHub-Event", "push");
    request.Headers.Add("X-Hub-Signature-256", "sha256=abc");

    return await client.SendAsync(request, Ct);
  }

  #endregion
}
