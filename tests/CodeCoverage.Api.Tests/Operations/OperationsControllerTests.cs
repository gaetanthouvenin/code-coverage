// ------------------------------------------------------------------------------------------------
// <copyright file="OperationsControllerTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Net;
using System.Text.Json;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

using CodeCoverage.Api.Tests.Support;
using CodeCoverage.Domain.Operations;
using CodeCoverage.Domain.Ports;

namespace CodeCoverage.Api.Tests.Operations;

// The operations of the instance through the API: an administrator of the instance with a token of the instance.
[Collection(CodeCoverageApiCollection.Name)]
public sealed class OperationsControllerTests
{
  #region Fields

  private static readonly DateTimeOffset Now = new(2026, 11, 12, 8, 0, 0, TimeSpan.Zero);

  private readonly CodeCoverageApiFactory _factory;

  private readonly IJobMonitor _monitor = A.Fake<IJobMonitor>();

  private readonly IJobQueue _jobQueue = A.Fake<IJobQueue>();

  private readonly IAuditLog _auditLog = A.Fake<IAuditLog>();

  #endregion

  #region Ctors

  public OperationsControllerTests(CodeCoverageApiFactory factory)
  {
    _factory = factory;

    var job = new JobSummary
    {
      Id = 5,
      Type = "FinalizeCommit",
      State = JobState.Dead,
      Attempts = 8,
      MaxAttempts = 8,
      RunAfter = Now,
      FirstQueuedAt = Now,
      HasError = true,
      CorrelationId = "c-1",
      CreatedAt = Now,
    };

    A.CallTo(() => _monitor.ListAsync(A<JobState?>._, A<int>._, A<int>._, A<CancellationToken>._))
     .ReturnsLazily((JobState? _, int page, int size, CancellationToken _) => new JobPage([job], page, size, 1));
    A.CallTo(() => _monitor.GetStatusAsync(A<CancellationToken>._))
     .Returns(new SystemStatus(1, 0, 0, 1, TimeSpan.FromSeconds(90), Now, new Dictionary<string, long> { ["42"] = 4000 }));
  }

  #endregion

  #region Properties

  private static CancellationToken Ct => TestContext.Current.CancellationToken;

  #endregion

  #region Methods

  [Fact]
  public async Task Get_Jobs_ShouldExposeMetadataOnly()
  {
    using var client = Client(organizationId: null);

    using var response = await client.GetAsync(new Uri("/api/v1/ops/jobs?state=dead", UriKind.Relative), Ct);
    var json = await response.Content.ReadAsStringAsync(Ct);
    using var body = JsonDocument.Parse(json);

    response.StatusCode.ShouldBe(HttpStatusCode.OK);
    body.RootElement.GetProperty("items")[0].GetProperty("hasError").GetBoolean().ShouldBeTrue();
    json.ShouldNotContain("lockKey", Case.Insensitive);
    json.ShouldNotContain("payload", Case.Insensitive);
    A.CallTo(() => _monitor.ListAsync(JobState.Dead, 1, 25, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
  }

  [Fact]
  public async Task Get_Status_ShouldReportTheQueueTheWorkerAndTheQuota()
  {
    using var client = Client(organizationId: null);

    using var response = await client.GetAsync(new Uri("/api/v1/ops/status", UriKind.Relative), Ct);
    using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));

    response.StatusCode.ShouldBe(HttpStatusCode.OK);
    body.RootElement.GetProperty("oldestQueuedAgeSeconds").GetDouble().ShouldBe(90);
    body.RootElement.GetProperty("gitHubRemaining").GetProperty("42").GetInt64().ShouldBe(4000);
  }

  [Fact]
  public async Task Post_Retry_ShouldReturn202()
  {
    A.CallTo(() => _jobQueue.RetryAsync(5, A<CancellationToken>._)).Returns(JobRetryResult.Retried);
    using var client = Client(organizationId: null);

    using var response = await client.PostAsync(new Uri("/api/v1/ops/jobs/5/retry", UriKind.Relative), null, Ct);

    response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
  }

  [Fact]
  public async Task Post_RetryOfARunningJob_ShouldReturn409()
  {
    A.CallTo(() => _jobQueue.RetryAsync(5, A<CancellationToken>._)).Returns(JobRetryResult.NotRetryable);
    using var client = Client(organizationId: null);

    using var response = await client.PostAsync(new Uri("/api/v1/ops/jobs/5/retry", UriKind.Relative), null, Ct);

    response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
  }

  // A token of an organization stays within it: the operations of the instance need a token of the instance.
  [Fact]
  public async Task Get_WithATokenOfAnOrganization_ShouldReturn403()
  {
    using var client = Client(organizationId: 3);

    using var response = await client.GetAsync(new Uri("/api/v1/ops/status", UriKind.Relative), Ct);

    response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
  }

  private HttpClient Client(long? organizationId) =>
    _factory.WithWebHostBuilder(b =>
             {
               // The user of the test tokens administers the instance.
               b.UseSetting("Access:AdminUserIds", ApiTokens.ExternalId);
               b.ConfigureTestServices(s =>
               {
                 s.AddApiToken(organizationId);
                 s.AddScoped(_ => _monitor);
                 s.AddScoped(_ => _jobQueue);
                 s.AddScoped(_ => _auditLog);
               });
             })
            .CreateClient()
            .WithApiToken();

  #endregion
}
