// ------------------------------------------------------------------------------------------------
// <copyright file="NotifyCommitIntegrationTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Collections.Concurrent;
using System.Globalization;
using System.Security.Cryptography;

using Mediator;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

using WireMock.Matchers;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;

using CodeCoverage.Application;
using CodeCoverage.Application.Configuration;
using CodeCoverage.Application.Features.Uploads.Commands.CompleteUploads;
using CodeCoverage.Application.Features.Uploads.Commands.ReceiveUpload;
using CodeCoverage.Domain.Commits;
using CodeCoverage.Domain.Contracts.Forges;
using CodeCoverage.Domain.Contracts.Jobs;
using CodeCoverage.Domain.Contracts.Uploads;
using CodeCoverage.Domain.Git;
using CodeCoverage.Domain.Notifications;
using CodeCoverage.Domain.Operations;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Domain.Repositories;
using CodeCoverage.Domain.Tokens;
using CodeCoverage.Infrastructure.GitHub;
using CodeCoverage.Infrastructure.Jobs;
using CodeCoverage.Infrastructure.Persistence;
using CodeCoverage.Infrastructure.PostgreSql;
using CodeCoverage.Infrastructure.Security;
using CodeCoverage.Infrastructure.SqlServer;
using CodeCoverage.Infrastructure.Tests.Support;

namespace CodeCoverage.Infrastructure.Tests.Notifications;

// From an upload to its published statuses, through the real parser, the real queue, the real worker and the real
// database: only the forge is faked, and it counts what it is asked to publish.
[Collection(DatabaseCollection.Name)]
public sealed class NotifyCommitIntegrationTests(DatabaseFixture database)
{
  #region Fields

  private const string TokenValue = "cvc_abcdefghijklmnopqrstuvwxyz0123456789ABCDEFG";

  private static readonly GitSha Sha = GitSha.Parse(new string('a', GitSha.Length));

  private static readonly DateTimeOffset Now = new(2026, 11, 9, 8, 0, 0, TimeSpan.Zero);

  private static readonly RSA Key = RSA.Create(2048);

  private readonly ConcurrentQueue<(GitSha Sha, CommitStatus Status, string? Url)> _published = new();

  #endregion

  #region Properties

  private static CancellationToken Ct => TestContext.Current.CancellationToken;

  #endregion

  #region Methods

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task Worker_UploadsDeclaredComplete_ShouldPublishTheStatusesOfTheCommitOnce(DatabaseProvider provider)
  {
    var seed = await SeededAsync(provider);
    await using var services = BuildServices(provider, seed.ConnectionString);

    await SendAsync(services);
    await RunUntilIdleAsync(services);

    var completion = await CompleteAsync(services);
    await RunUntilIdleAsync(services);

    completion.ShouldBe(new UploadCompletion(1, 1, 0, 0));
    _published.Select(static p => (p.Sha, p.Status.Context, p.Status.State, p.Status.Description))
              .ShouldBe(
                [
                  (Sha, "coverage/project", StatusState.Success, "No report found to compare against"),
                  (Sha, "coverage/patch", StatusState.Success, "Coverage not affected"),
                ],
                ignoreOrder: true
              );
    _published.ShouldAllBe(static p => p.Url == $"https://coverage.example.com/github/first-org/app/commit/{Sha.Value}");

    await using var first = seed.ContextOf(seed.FirstId);
    var commit = await first.Commits.AsNoTracking().SingleAsync(Ct);
    commit.NotifiedAt.ShouldNotBeNull();
    (await first.PostedStatuses.AsNoTracking().Select(static s => s.Context).ToListAsync(Ct)).ShouldBe(["coverage/project", "coverage/patch"], ignoreOrder: true);
    (await first.Comparisons.AsNoTracking().CountAsync(Ct)).ShouldBe(1);

    // Nothing of the other organization was written.
    await using var second = seed.ContextOf(seed.SecondId);
    (await second.PostedStatuses.AsNoTracking().CountAsync(Ct)).ShouldBe(0);
  }

  // Two notifications of the same commit one after the other: the second one publishes nothing.
  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task Worker_NotifiedTwiceInARow_ShouldNotPublishAStatusTwice(DatabaseProvider provider)
  {
    var seed = await SeededAsync(provider);
    await using var services = BuildServices(provider, seed.ConnectionString);

    await SendAsync(services);
    await RunUntilIdleAsync(services);
    await CompleteAsync(services);
    await RunUntilIdleAsync(services);
    _published.Count.ShouldBe(2);

    await CompleteAsync(services);
    await RunUntilIdleAsync(services);

    _published.Count.ShouldBe(2);

    await using var check = seed.ContextWithoutOrganization();
    var jobs = await check.Jobs.AsNoTracking().Where(static j => j.Type == JobTypes.NotifyCommit && j.PayloadJson.Contains("true")).ToListAsync(Ct);
    jobs.Count.ShouldBe(2);
    jobs.ShouldAllBe(static j => j.State == JobState.Succeeded);
  }

  // The notification of the finalization and the one asked by hand are both due, and two workers take them at the
  // same time: the lock of the commit runs them one after the other, and the second one finds the statuses published.
  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task Worker_TwoNotificationsAtTheSameTime_ShouldNotPublishAStatusTwice(DatabaseProvider provider)
  {
    var seed = await SeededAsync(provider);
    await using var services = BuildServices(provider, seed.ConnectionString);

    await SendAsync(services);
    await RunUntilIdleAsync(services);
    await CompleteAsync(services);

    await using (var context = seed.ContextWithoutOrganization())
    {
      // The debounce of the notification queued by the finalization is over.
      var past = DateTimeOffset.UtcNow.AddMinutes(-1);
      await context.Jobs.Where(static j => j.Type == JobTypes.NotifyCommit).ExecuteUpdateAsync(s => s.SetProperty(static j => j.RunAfter, past), Ct);
      (await context.Jobs.CountAsync(static j => j.Type == JobTypes.NotifyCommit && j.State == JobState.Queued, Ct)).ShouldBe(2);
    }

    var worker = Worker(services);
    var ran = await Task.WhenAll(worker.RunOnceAsync("worker-1", Ct), worker.RunOnceAsync("worker-2", Ct));

    // One of the two is held back by the lock of the commit: it never runs beside the other.
    ran.Count(static r => r).ShouldBe(1);

    await RunUntilIdleAsync(services);

    _published.Count.ShouldBe(2);
    _published.Select(static p => p.Status.Context).ShouldBeUnique();

    await using var check = seed.ContextOf(seed.FirstId);
    (await check.PostedStatuses.AsNoTracking().CountAsync(Ct)).ShouldBe(2);

    await using var jobs = seed.ContextWithoutOrganization();
    (await jobs.Jobs.AsNoTracking().Where(static j => j.Type == JobTypes.NotifyCommit).ToListAsync(Ct)).ShouldAllBe(static j => j.State == JobState.Succeeded);
  }

  // The debounced notification queued by the finalization publishes too, once its delay is over.
  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task Worker_NotificationOfTheFinalization_ShouldPublishOnceItsDebounceIsOver(DatabaseProvider provider)
  {
    var seed = await SeededAsync(provider);
    await using var services = BuildServices(provider, seed.ConnectionString);

    await SendAsync(services);
    await RunUntilIdleAsync(services);

    // Still waiting: nothing is published during the debounce.
    _published.ShouldBeEmpty();

    await using (var context = seed.ContextWithoutOrganization())
    {
      var job = await context.Jobs.AsNoTracking().SingleAsync(static j => j.Type == JobTypes.NotifyCommit, Ct);
      job.State.ShouldBe(JobState.Queued);
      job.OrganizationId.ShouldBe(seed.FirstId);

      var past = DateTimeOffset.UtcNow.AddMinutes(-1);
      await context.Jobs.Where(static j => j.Type == JobTypes.NotifyCommit).ExecuteUpdateAsync(s => s.SetProperty(static j => j.RunAfter, past), Ct);
    }

    await RunUntilIdleAsync(services);

    _published.Count.ShouldBe(2);
  }

  // The acceptance of M2.23: a commit that waits for uploads that never come gets no pending status. Once the timeout
  // is over, the sweep notifies it by force, and its statuses say they rest on a part of the uploads.
  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task Worker_StuckCommit_ShouldBeNotifiedByTheSweepWithAPartialDescription(DatabaseProvider provider)
  {
    var seed = await SeededAsync(provider);
    await using var services = BuildServices(provider, seed.ConnectionString, "codecov:\n  notify:\n    after_n_builds: 2\n");

    await SendAsync(services);
    await RunUntilIdleAsync(services);

    // One upload out of the two expected: nothing is notified, and nothing is queued to notify it.
    _published.ShouldBeEmpty();

    await using (var waiting = seed.ContextWithoutOrganization())
    {
      (await waiting.Jobs.AsNoTracking().CountAsync(static j => j.Type == JobTypes.NotifyCommit, Ct)).ShouldBe(0);
    }

    await SweepAsync(services);
    await RunUntilIdleAsync(services);

    // Its last upload is recent: the sweep leaves it alone.
    _published.ShouldBeEmpty();

    await using (var aging = seed.ContextOf(seed.FirstId))
    {
      var longAgo = DateTimeOffset.UtcNow.AddHours(-2);
      await aging.Commits.ExecuteUpdateAsync(s => s.SetProperty(static c => c.LastUploadAt, longAgo), Ct);
    }

    await SweepAsync(services);
    await RunUntilIdleAsync(services);

    _published.Count.ShouldBe(2);
    _published.ShouldAllBe(static p => p.Status.Description.EndsWith(" (partial: 1/2 uploads)", StringComparison.Ordinal));
    _published.ShouldAllBe(static p => p.Status.State == StatusState.Success || p.Status.State == StatusState.Failure);

    await using (var first = seed.ContextOf(seed.FirstId))
    {
      (await first.Commits.AsNoTracking().SingleAsync(Ct)).NotifiedAt.ShouldNotBeNull();
    }

    // Notified once: the next sweep finds nothing left.
    await SweepAsync(services);
    await RunUntilIdleAsync(services);

    _published.Count.ShouldBe(2);

    await using var check = seed.ContextWithoutOrganization();
    var jobs = await check.Jobs.AsNoTracking().OrderBy(static j => j.Id).ToListAsync(Ct);
    jobs.ShouldAllBe(static j => j.State == JobState.Succeeded);
    jobs.Count(static j => j.Type == JobTypes.NotifyCommit).ShouldBe(1);
    // A coordinator on the lane of the instance, then one sweep for the only active organization, three times.
    jobs.Where(static j => j.Type == JobTypes.SweepPendingCommits).Select(static j => j.OrganizationId)
        .ShouldBe([null, seed.FirstId, null, seed.FirstId, null, seed.FirstId]);
  }

  // T1. Statuses GitHub refuses for good, through the real gateway (a 403 without a rate limit, a 404, a 422): the
  // notification of the sweep fails at once without a retry, the commit is marked, and the next sweep leaves it alone.
  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task Worker_StuckCommitWhoseStatusesGitHubRefusesForGood_ShouldBeMarkedOnceAndNotForcedAgain(DatabaseProvider provider)
  {
    foreach (var refusal in new[] { 403, 404, 422 })
    {
      _published.Clear();
      using var github = RefusingGitHub(refusal);
      var seed = await SeededAsync(provider);
      await using var services = BuildServices(provider, seed.ConnectionString, "codecov:\n  notify:\n    after_n_builds: 2\n", github.Gateway);

      await SendAsync(services);
      await RunUntilIdleAsync(services);

      await using (var aging = seed.ContextOf(seed.FirstId))
      {
        var longAgo = DateTimeOffset.UtcNow.AddHours(-2);
        await aging.Commits.ExecuteUpdateAsync(s => s.SetProperty(static c => c.LastUploadAt, longAgo), Ct);
      }

      await SweepAsync(services);
      await RunUntilIdleAsync(services);

      await using (var first = seed.ContextOf(seed.FirstId))
      {
        (await first.Commits.AsNoTracking().SingleAsync(Ct)).NotifiedAt.ShouldNotBeNull($"HTTP {refusal}");
      }

      await SweepAsync(services);
      await RunUntilIdleAsync(services);

      _published.ShouldBeEmpty();
      github.Server.LogEntries.Count(static e => e.RequestMessage!.Path.Contains("/statuses/", StringComparison.Ordinal)).ShouldBe(1, $"HTTP {refusal}");

      await using var check = seed.ContextWithoutOrganization();
      var notification = (await check.Jobs.AsNoTracking().Where(static j => j.Type == JobTypes.NotifyCommit).ToListAsync(Ct)).ShouldHaveSingleItem($"HTTP {refusal}");
      notification.State.ShouldBe(JobState.Failed);
      notification.Attempts.ShouldBe(1);
    }
  }

  // T2. Under manual_trigger, a commit its pipeline notified is asked again (its base got its report) while one more
  // upload is in progress: that job is dropped, and the finalization of the upload notifies the commit again.
  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task Worker_NotificationDroppedWhileAnUploadIsInProgress_ShouldBeQueuedAgainByItsFinalization(DatabaseProvider provider)
  {
    var seed = await SeededAsync(provider);
    await using var services = BuildServices(provider, seed.ConnectionString, "codecov:\n  notify:\n    manual_trigger: true\n");

    await SendAsync(services);
    await RunUntilIdleAsync(services);
    _published.ShouldBeEmpty();

    await CompleteAsync(services);
    await RunUntilIdleAsync(services);
    _published.Count.ShouldBe(2);
    var triggered = await NotifiedAtAsync(seed);

    // One more upload, still waiting to be processed, and the notification asked when the base got its report.
    await SendAsync(services, "second");
    await DelayUploadsAsync(seed, DateTimeOffset.UtcNow.AddHours(1));
    await QueueNotificationAsync(seed);
    await RunUntilIdleAsync(services);

    // Dropped: the upload in progress would not be in the report.
    (await NotifiedAtAsync(seed)).ShouldBe(triggered);

    await DelayUploadsAsync(seed, DateTimeOffset.UtcNow.AddMinutes(-1));
    await RunUntilIdleAsync(services);

    await using (var context = seed.ContextWithoutOrganization())
    {
      // The finalization queued its debounced notification; its debounce is over.
      var past = DateTimeOffset.UtcNow.AddMinutes(-1);
      (await context.Jobs.CountAsync(static j => j.Type == JobTypes.NotifyCommit && j.State == JobState.Queued, Ct)).ShouldBe(1);
      await context.Jobs.Where(static j => j.Type == JobTypes.NotifyCommit && j.State == JobState.Queued).ExecuteUpdateAsync(s => s.SetProperty(static j => j.RunAfter, past), Ct);
    }

    await RunUntilIdleAsync(services);

    (await NotifiedAtAsync(seed)).ShouldNotBeNull().ShouldBeGreaterThan(triggered!.Value);

    await using var check = seed.ContextWithoutOrganization();
    (await check.Jobs.AsNoTracking().ToListAsync(Ct)).ShouldAllBe(static j => j.State == JobState.Succeeded);
  }

  private static GitHubApiHarness RefusingGitHub(int status)
  {
    var github = new GitHubApiHarness();

    github.UsePrivateKey(Key);
    github.Server
          .Given(Request.Create().WithPath(new RegexMatcher("^/app/installations/[0-9]+/access_tokens$")).UsingPost())
          .RespondWith(
            Response.Create()
                    .WithStatusCode(201)
                    .WithBody("{\"token\":\"ghs_installation\",\"expires_at\":\"" + DateTimeOffset.UtcNow.AddHours(1).ToString("O", CultureInfo.InvariantCulture) + "\"}")
          );
    github.Server
          .Given(Request.Create().WithPath(new RegexMatcher("^/repositories/[0-9]+/statuses/[0-9a-f]+$")).UsingPost())
          .RespondWith(Response.Create().WithStatusCode(status).WithBody("{}"));

    return github;
  }

  private static async Task<DateTimeOffset?> NotifiedAtAsync(TwoOrganizationDatabase seed)
  {
    await using var context = seed.ContextOf(seed.FirstId);

    return (await context.Commits.AsNoTracking().SingleAsync(Ct)).NotifiedAt;
  }

  private static async Task DelayUploadsAsync(TwoOrganizationDatabase seed, DateTimeOffset runAfter)
  {
    await using var context = seed.ContextWithoutOrganization();

    await context.Jobs.Where(static j => j.Type == JobTypes.ProcessUpload && j.State == JobState.Queued)
                 .ExecuteUpdateAsync(s => s.SetProperty(static j => j.RunAfter, runAfter), Ct);
  }

  // As the finalization of the base queues it for its dependents: not forced, at once.
  private static async Task QueueNotificationAsync(TwoOrganizationDatabase seed)
  {
    await using var context = seed.ContextWithoutOrganization();
    var forced = await context.Jobs.AsNoTracking().SingleAsync(static j => j.Type == JobTypes.NotifyCommit, Ct);
    await using var first = seed.ContextOf(seed.FirstId);
    var commitId = (await first.Commits.AsNoTracking().SingleAsync(Ct)).Id;
    var now = DateTimeOffset.UtcNow;

    context.Jobs.Add(
      new JobRecord
      {
        OrganizationId = forced.OrganizationId,
        Type = JobTypes.NotifyCommit,
        PayloadJson = string.Create(CultureInfo.InvariantCulture, $"{{\"CommitId\":{commitId},\"Force\":false}}"),
        LockKey = forced.LockKey,
        State = JobState.Queued,
        MaxAttempts = forced.MaxAttempts,
        RunAfter = now.AddMinutes(-1),
        FirstQueuedAt = now,
        CorrelationId = Guid.NewGuid().ToString("N"),
        CreatedAt = now,
      }
    );
    await context.SaveChangesAsync(Ct);
  }

  private static async Task SweepAsync(ServiceProvider services)
  {
    await using var scope = services.CreateAsyncScope();

    // As the recurring job scheduler queues it, every 5 minutes.
    var coordinator = JobRequest.ForInstance(JobTypes.SweepPendingCommits, "{}", SweepPendingCommitsJobs.Key, SweepPendingCommitsJobs.Key, coalesce: true);

    await scope.ServiceProvider.GetRequiredService<IJobQueue>().EnqueueAsync(coordinator, Ct);
  }

  private static JobWorker Worker(IServiceProvider services) => services.GetServices<IHostedService>().OfType<JobWorker>().Single();

  private static async Task RunUntilIdleAsync(ServiceProvider services)
  {
    var worker = Worker(services);

    for (var round = 0; round < 50; round++)
    {
      if (!await worker.RunOnceAsync("worker-1", Ct))
      {
        return;
      }
    }

    throw new InvalidOperationException("The queue never emptied.");
  }

  private static async Task SendAsync(ServiceProvider services, string name = "only")
  {
    await using var scope = services.CreateAsyncScope();

    var report = await File.ReadAllBytesAsync(Path.Combine(AppContext.BaseDirectory, "fixtures", "reports", "cobertura", "coverlet-async.xml"), Ct);
    var metadata = new UploadMetadata(RepositorySlug.Parse("first-org/app"), Sha, "main", null, [], name, null, null, new CiContext(null, null, null, name));
    var command = new ReceiveUploadCommand(new UploadCredential(UploadCredentialKind.UploadToken, TokenValue), metadata, Parts(report), name);

    await scope.ServiceProvider.GetRequiredService<ISender>().Send(command, Ct);
  }

  private static async Task<UploadCompletion> CompleteAsync(ServiceProvider services)
  {
    await using var scope = services.CreateAsyncScope();

    var command = new CompleteUploadsCommand(new UploadCredential(UploadCredentialKind.UploadToken, TokenValue), Forge.GitHub, "first-org", "app", Sha.Value);

    return await scope.ServiceProvider.GetRequiredService<ISender>().Send(command, Ct);
  }

  private static async IAsyncEnumerable<UploadPart> Parts(byte[] report)
  {
    await Task.CompletedTask;

    yield return new UploadPart(UploadPartKind.Report, "coverage.cobertura.xml", new MemoryStream(report));
  }

  private ServiceProvider BuildServices(DatabaseProvider provider, string connectionString, string? yaml = null, GitHubAppGateway? github = null)
  {
    var configuration = new ConfigurationBuilder()
                        .AddInMemoryCollection(new Dictionary<string, string?>
                        {
                          ["ConnectionStrings:CodeCoverage"] = connectionString,
                          ["CodeCoverage:Database:Provider"] = provider.ToString(),
                        })
                        .Build();
    var services = new ServiceCollection().AddSingleton<IConfiguration>(configuration);

    services.AddLogging().AddApplication().AddInfrastructure().AddPostgreSqlProvider().AddSqlServerProvider().AddJobProcessing();

    var gateway = A.Fake<IForgeGateway>();
    A.CallTo(() => gateway.GetRepositoryConnectionAsync(A<string>._, A<string>._, A<CancellationToken>._)).Returns(new ForgeConnectionInfo("77", "1001", "4242"));
    A.CallTo(() => gateway.GetRepositoryAsync(A<ForgeRepositoryRef>._, A<CancellationToken>._)).Returns(new ForgeRepositoryInfo("4242", "first-org", string.Empty, "app", "main", true));
    // The tree of the root, where the configuration is looked for; the whole tree, which maps the paths of a report,
    // stays unknown.
    A.CallTo(() => gateway.GetTreeAsync(A<ForgeRepositoryRef>._, A<GitSha>._, A<bool>._, A<CancellationToken>._)).Returns((ForgeTree?)null);

    if (yaml is not null)
    {
      A.CallTo(() => gateway.GetTreeAsync(A<ForgeRepositoryRef>._, A<GitSha>._, false, A<CancellationToken>._))
       .Returns(new ForgeTree(Sha, [new ForgeTreeEntry("codecov.yml", Sha, false)], false));
    }

    A.CallTo(() => gateway.GetContentAsync(A<ForgeRepositoryRef>._, A<string>._, A<GitSha>._, A<CancellationToken>._)).Returns(yaml);
    A.CallTo(() => gateway.ListCommitsAsync(A<ForgeRepositoryRef>._, A<GitSha>._, A<int>._, A<CancellationToken>._)).Returns([]);
    A.CallTo(() => gateway.GetPullAsync(A<ForgeRepositoryRef>._, A<int>._, A<CancellationToken>._)).Returns((ForgePullInfo?)null);
    A.CallTo(() => gateway.CreateStatusAsync(A<ForgeRepositoryRef>._, A<GitSha>._, A<CommitStatus>._, A<string?>._, A<CancellationToken>._))
     .ReturnsLazily(
       async (ForgeRepositoryRef repository, GitSha sha, CommitStatus status, string? url, CancellationToken cancellationToken) =>
       {
         // The statuses go through the real gateway when a test gives one: what GitHub answers is what it maps.
         if (github is not null)
         {
           await github.CreateStatusAsync(repository, sha, status, url, cancellationToken);
         }

         _published.Enqueue((sha, status, url));
       }
     );

    var gateways = A.Fake<IForgeGatewayProvider>();
    A.CallTo(() => gateways.GetGateway(A<Forge>._)).Returns(gateway);
    services.RemoveAll<IForgeGatewayProvider>();
    services.AddSingleton(gateways);

    // The public address of the Web console is a setting of the instance.
    var urls = A.Fake<IOptionsMonitor<UrlOptions>>();
    A.CallTo(() => urls.CurrentValue).Returns(new UrlOptions { WebPublicUrl = "https://coverage.example.com" });
    services.RemoveAll<IOptionsMonitor<UrlOptions>>();
    services.AddSingleton(urls);

    var protector = A.Fake<ISecretProtector>();
    A.CallTo(() => protector.Protect(A<string>._)).Returns("protected");
    services.AddSingleton(protector);

    return services.BuildServiceProvider();
  }

  private async Task<TwoOrganizationDatabase> SeededAsync(DatabaseProvider provider)
  {
    var seed = await TwoOrganizationDatabase.CreateAsync(database, provider, Ct);

    await using var context = seed.ContextOf(seed.FirstId);
    var organization = await context.Organizations.SingleAsync(o => o.Id == seed.FirstId, Ct);

    organization.Admit("root", Now);
    context.UploadTokens.Add(UploadToken.Create(UploadTokenScope.Organization, null, "ci", "hint", new Sha256TokenHasher().Hash(TokenValue), false, "root", Now, null));
    await context.SaveChangesAsync(Ct);

    return seed;
  }

  #endregion
}