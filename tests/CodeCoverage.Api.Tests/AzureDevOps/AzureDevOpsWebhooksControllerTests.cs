// ------------------------------------------------------------------------------------------------
// <copyright file="AzureDevOpsWebhooksControllerTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Net;
using System.Text;
using System.Text.Json;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

using CodeCoverage.Api.Tests.Support;
using CodeCoverage.Domain.Connections;
using CodeCoverage.Domain.Contracts.Forges;
using CodeCoverage.Domain.Contracts.Jobs;
using CodeCoverage.Domain.Contracts.Webhooks;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Ports;

namespace CodeCoverage.Api.Tests.AzureDevOps;

[Collection(CodeCoverageApiCollection.Name)]
public sealed class AzureDevOpsWebhooksControllerTests(CodeCoverageApiFactory factory)
{
  #region Fields

  private const string ConnectionId = "conn-1";

  private const string WebhooksPath = CodeCoverageApi.Prefix + "/azure-devops/webhooks/" + ConnectionId;

  private const string Secret = "connection-secret";

  private const string EventId = "0a6f5cbb-6b0a-4f8e-b0a4-3a3e1b7f1d2c";

  private const string Body = $$$$"""{"id":"{{{{EventId}}}}","eventType":"git.push","resource":{"repository":{"id":"r-1"}}}""";

  private readonly IForgeConnectionRepository _connections = A.Fake<IForgeConnectionRepository>();

  private readonly IWebhookDeliveryRepository _deliveries = A.Fake<IWebhookDeliveryRepository>();

  private readonly IForgeGateway _gateway = A.Fake<IForgeGateway>();

  #endregion

  #region Properties

  private static CancellationToken Ct => TestContext.Current.CancellationToken;

  #endregion

  #region Methods

  [Fact]
  public async Task Post_ValidSecret_ShouldReturnAccepted()
  {
    await using var host = Host();
    using var client = host.CreateClient();
    ArrangeConnection(host);
    A.CallTo(() => _deliveries.TryStoreForgeDeliveryAsync(A<ForgeDelivery>._, A<JobRequest>._, A<DateTimeOffset>._, A<CancellationToken>._))
     .Returns(true);

    using var response = await PostAsync(client, Body, Secret);

    response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
    A.CallTo(() => _deliveries.TryStoreForgeDeliveryAsync(
                   A<ForgeDelivery>.That.Matches(static d => d.Forge == Forge.AzureDevOps
                                                             && d.DeliveryId == EventId
                                                             && d.Event == "git.push"
                                                             && d.ConnectionId == ConnectionId
                                                             && Encoding.UTF8.GetString(d.Payload) == Body),
                   A<JobRequest>._,
                   A<DateTimeOffset>._,
                   A<CancellationToken>._))
     .MustHaveHappenedOnceExactly();

    // The request never calls Azure DevOps: the job reads the state again, later.
    A.CallTo(_gateway).MustNotHaveHappened();
  }

  [Fact]
  public async Task Post_WrongSecret_ShouldReturnUnauthorized()
  {
    await using var host = Host();
    using var client = host.CreateClient();
    ArrangeConnection(host);

    foreach (var secret in new[] { "wrong-secret", null })
    {
      using var response = await PostAsync(client, Body, secret);
      using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));

      response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized, $"secret: {secret ?? "(none)"}");
      problem.RootElement.GetProperty("errorCode").GetString().ShouldBe("Webhook.InvalidSignature");
    }

    A.CallTo(_deliveries).MustNotHaveHappened();
    A.CallTo(_gateway).MustNotHaveHappened();
  }

  [Fact]
  public async Task Post_SameDeliveryTwice_ShouldStoreOnce()
  {
    await using var host = Host();
    using var client = host.CreateClient();
    ArrangeConnection(host);
    HashSet<string> stored = [];
    A.CallTo(() => _deliveries.TryStoreForgeDeliveryAsync(A<ForgeDelivery>._, A<JobRequest>._, A<DateTimeOffset>._, A<CancellationToken>._))
     .ReturnsLazily((ForgeDelivery delivery, JobRequest _, DateTimeOffset _, CancellationToken _) => stored.Add(delivery.DeliveryId));

    using var first = await PostAsync(client, Body, Secret);
    using var second = await PostAsync(client, Body, Secret);

    first.StatusCode.ShouldBe(HttpStatusCode.Accepted);
    second.StatusCode.ShouldBe(HttpStatusCode.OK);
    stored.ShouldBe([EventId]);
  }

  // The identifier of the delivery and its event are read from the body: a body without them is refused before the
  // connection is even read.
  [Fact]
  public async Task Post_BodyWithoutEventIdentity_ShouldReturnBadRequest()
  {
    await using var host = Host();
    using var client = host.CreateClient();
    ArrangeConnection(host);

    string[] bodies =
    [
      "not json",
      "[]",
      "{}",
      """{"id":1,"eventType":"git.push"}""",
      $$"""{"id":"{{EventId}}","eventType":true}""",
    ];

    foreach (var body in bodies)
    {
      using var response = await PostAsync(client, body, Secret);

      response.StatusCode.ShouldBe(HttpStatusCode.BadRequest, body);
    }

    A.CallTo(_connections).MustNotHaveHappened();
    A.CallTo(_deliveries).MustNotHaveHappened();
  }

  private void ArrangeConnection(WebApplicationFactory<Program> host)
  {
    var protector = host.Services.GetRequiredService<ISecretProtector>();
    var owner = new ForgeConnectionOwner(9, Forge.AzureDevOps, OrganizationState.Active, ForgeConnectionStatus.Active, protector.Protect(Secret), null);

    A.CallTo(() => _connections.FindOwnerAsync(ConnectionId, A<CancellationToken>._)).Returns(owner);
  }

  private WebApplicationFactory<Program> Host() =>
    factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
    {
      services.AddSingleton(_connections);
      services.AddSingleton(_deliveries);
      services.AddKeyedSingleton(Forge.AzureDevOps, _gateway);
    }));

  private static async Task<HttpResponseMessage> PostAsync(HttpClient client, string body, string? secret)
  {
    using var request = new HttpRequestMessage(HttpMethod.Post, WebhooksPath)
    {
      Content = new StringContent(body, Encoding.UTF8, "application/json"),
    };

    if (secret is not null)
    {
      request.Headers.Add(AzureDevOpsWebhookHeaders.Secret, secret);
    }

    return await client.SendAsync(request, Ct);
  }

  #endregion
}
