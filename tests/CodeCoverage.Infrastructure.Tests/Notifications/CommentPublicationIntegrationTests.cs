// ------------------------------------------------------------------------------------------------
// <copyright file="CommentPublicationIntegrationTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;

using Mediator;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;

using CodeCoverage.Application;
using CodeCoverage.Application.Configuration;
using CodeCoverage.Application.Features.Uploads.Commands.CompleteUploads;
using CodeCoverage.Application.Features.Uploads.Commands.ReceiveUpload;
using CodeCoverage.Domain.Contracts.Forges;
using CodeCoverage.Domain.Contracts.Jobs;
using CodeCoverage.Domain.Contracts.Uploads;
using CodeCoverage.Domain.Git;
using CodeCoverage.Domain.Operations;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Domain.Repositories;
using CodeCoverage.Domain.Tokens;
using CodeCoverage.Infrastructure.Jobs;
using CodeCoverage.Infrastructure.Persistence;
using CodeCoverage.Infrastructure.PostgreSql;
using CodeCoverage.Infrastructure.Security;
using CodeCoverage.Infrastructure.SqlServer;
using CodeCoverage.Infrastructure.Tests.Support;

namespace CodeCoverage.Infrastructure.Tests.Notifications;

// The acceptance of M2.22: a pull request never gets two comments, even when the save that records the first one
// loses a race. From an upload to the comment, through the real queue, the real worker, the real database and the
// real GitHub gateway against a WireMock server: only the readings of the forge that are not about comments are faked.
[Collection(DatabaseCollection.Name)]
public sealed class CommentPublicationIntegrationTests(DatabaseFixture database) : IDisposable
{
  #region Fields

  private const string TokenValue = "cvc_abcdefghijklmnopqrstuvwxyz0123456789ABCDEFG";

  private const string Comments = "/repositories/4242/issues/12/comments";

  private const long CommentId = 9001;

  private static readonly RSA Key = RSA.Create(2048);

  private static readonly GitSha Sha = GitSha.Parse(new string('a', GitSha.Length));

  private static readonly GitSha BaseSha = GitSha.Parse(new string('b', GitSha.Length));

  private static readonly DateTimeOffset Now = new(2026, 11, 9, 8, 0, 0, TimeSpan.Zero);

  private readonly GitHubApiHarness _github = new();

  #endregion

  #region Properties

  private static CancellationToken Ct => TestContext.Current.CancellationToken;

  #endregion

  #region Methods

  // The collision is forced: right after GitHub took the comment, and before the notification saves its identifier,
  // another writer changes the row of the pull request, as a synchronization would. Ten rounds on each engine.
  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task Worker_SaveOfTheCommentLosesARace_ShouldFindItAgainAndNeverWriteASecondOne(DatabaseProvider provider)
  {
    for (var round = 1; round <= 10; round++)
    {
      var seed = await SeededAsync(provider);
      var collide = true;

      _github.Server.ResetLogEntries();
      GivenGitHub(comments: "[]");

      await using var services = BuildServices(
        provider,
        seed.ConnectionString,
        async () =>
        {
          if (!collide)
          {
            return;
          }

          collide = false;

          await using var other = seed.ContextOf(seed.FirstId);
          await other.PullRequests.ExecuteUpdateAsync(static s => s.SetProperty(static p => p.Version, static p => p.Version + 1), Ct);
        }
      );

      await SendAsync(services);
      await RunUntilIdleAsync(services);
      await CompleteAsync(services);
      await RunUntilIdleAsync(services);

      // GitHub holds the comment, the database does not know it: the notification failed on its save and waits.
      Posted().Count.ShouldBe(1, $"round {round}");

      await using (var failed = seed.ContextOf(seed.FirstId))
      {
        (await failed.PullRequests.AsNoTracking().SingleAsync(Ct)).CommentId.ShouldBeNull();
      }

      // What GitHub now lists: the comment the application wrote, under its bot account.
      GivenGitHub(comments: JsonSerializer.Serialize(new[] { new { id = CommentId, body = Posted()[0], user = new { login = "code-coverage[bot]" } } }));
      await MakeNotificationsDueAsync(seed);
      await RunUntilIdleAsync(services);

      Posted().Count.ShouldBe(1, $"round {round}: a second comment was written");

      await using var first = seed.ContextOf(seed.FirstId);
      var pull = await first.PullRequests.AsNoTracking().SingleAsync(Ct);
      pull.CommentId.ShouldBe(CommentId);
      pull.CommentedHeadSha.ShouldBe(Sha);
      pull.CommentBodyHash.ShouldBe(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(Posted()[0])));

      await using var check = seed.ContextWithoutOrganization();
      (await check.Jobs.AsNoTracking().Where(static j => j.Type == JobTypes.NotifyCommit).ToListAsync(Ct)).ShouldAllBe(static j => j.State == JobState.Succeeded);
    }
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task Worker_NotifiedTwice_ShouldWriteTheCommentOnceAndLeaveItWhenNothingChanged(DatabaseProvider provider)
  {
    var seed = await SeededAsync(provider);
    GivenGitHub(comments: "[]");
    await using var services = BuildServices(provider, seed.ConnectionString, static () => Task.CompletedTask);

    await SendAsync(services);
    await RunUntilIdleAsync(services);
    await CompleteAsync(services);
    await RunUntilIdleAsync(services);
    await CompleteAsync(services);
    await MakeNotificationsDueAsync(seed);
    await RunUntilIdleAsync(services);

    var body = Posted().ShouldHaveSingleItem();
    body.ShouldStartWith("<!-- code-coverage:comment -->\n## [Coverage](https://coverage.example.com/github/first-org/app/pull/12) Report\n");
    _github.Server.LogEntries.ShouldNotContain(static e => e.RequestMessage!.Method == "PATCH");

    await using var first = seed.ContextOf(seed.FirstId);
    var pull = await first.PullRequests.AsNoTracking().SingleAsync(Ct);
    pull.CommentId.ShouldBe(CommentId);
    pull.ForkPointSha.ShouldBe(BaseSha);

    // Nothing of the other organization was written.
    await using var second = seed.ContextOf(seed.SecondId);
    (await second.PullRequests.AsNoTracking().CountAsync(Ct)).ShouldBe(0);
  }

  private static async Task MakeNotificationsDueAsync(TwoOrganizationDatabase seed)
  {
    await using var context = seed.ContextWithoutOrganization();
    var past = DateTimeOffset.UtcNow.AddMinutes(-1);

    await context.Jobs
                 .Where(static j => j.Type == JobTypes.NotifyCommit && j.State == JobState.Queued)
                 .ExecuteUpdateAsync(s => s.SetProperty(static j => j.RunAfter, past), Ct);
  }

  private static async Task RunUntilIdleAsync(ServiceProvider services)
  {
    var worker = services.GetServices<IHostedService>().OfType<JobWorker>().Single();

    for (var round = 0; round < 50; round++)
    {
      if (!await worker.RunOnceAsync("worker-1", Ct))
      {
        return;
      }
    }

    throw new InvalidOperationException("The queue never emptied.");
  }

  private static async Task SendAsync(ServiceProvider services)
  {
    await using var scope = services.CreateAsyncScope();

    var report = await File.ReadAllBytesAsync(Path.Combine(AppContext.BaseDirectory, "fixtures", "reports", "cobertura", "coverlet-async.xml"), Ct);
    var metadata = new UploadMetadata(RepositorySlug.Parse("first-org/app"), Sha, "feature/x", 12, [], "only", null, null, new CiContext(null, null, null, "only"));
    var command = new ReceiveUploadCommand(new UploadCredential(UploadCredentialKind.UploadToken, TokenValue), metadata, Parts(report), "only");

    await scope.ServiceProvider.GetRequiredService<ISender>().Send(command, Ct);
  }

  private static async Task CompleteAsync(ServiceProvider services)
  {
    await using var scope = services.CreateAsyncScope();

    var command = new CompleteUploadsCommand(new UploadCredential(UploadCredentialKind.UploadToken, TokenValue), Forge.GitHub, "first-org", "app", Sha.Value);

    await scope.ServiceProvider.GetRequiredService<ISender>().Send(command, Ct);
  }

  private static async IAsyncEnumerable<UploadPart> Parts(byte[] report)
  {
    await Task.CompletedTask;

    yield return new UploadPart(UploadPartKind.Report, "coverage.cobertura.xml", new MemoryStream(report));
  }

  // The bodies GitHub was asked to publish as new comments.
  private List<string> Posted() =>
  [
    .. _github.Server.LogEntries
              .Select(static e => e.RequestMessage!)
              .Where(static r => r.Path == Comments && r.Method == "POST")
              .Select(static r => JsonDocument.Parse(r.Body!).RootElement.GetProperty("body").GetString()!),
  ];

  // The mappings are replaced, the requests already logged are kept.
  private void GivenGitHub(string comments)
  {
    _github.Server.ResetMappings();
    _github.UsePrivateKey(Key);
    _github.Options.AppSlug = "code-coverage";
    _github.Server
           .Given(Request.Create().WithPath("/app/installations/77/access_tokens").UsingPost())
           .RespondWith(
             Response.Create()
                     .WithStatusCode(201)
                     .WithBody("{\"token\":\"ghs_installation\",\"expires_at\":\"" + DateTimeOffset.UtcNow.AddHours(1).ToString("O", CultureInfo.InvariantCulture) + "\"}")
           );
    _github.Server.Given(Request.Create().WithPath(Comments).UsingGet()).RespondWith(Response.Create().WithStatusCode(200).WithBody(comments));
    _github.Server.Given(Request.Create().WithPath(Comments).UsingPost())
           .RespondWith(Response.Create().WithStatusCode(201).WithBody("{\"id\":" + CommentId.ToString(CultureInfo.InvariantCulture) + "}"));
    _github.Server.Given(Request.Create().WithPath($"/repositories/4242/issues/comments/{CommentId}").UsingPatch())
           .RespondWith(Response.Create().WithStatusCode(200).WithBody("{\"id\":" + CommentId.ToString(CultureInfo.InvariantCulture) + "}"));
  }

  private ServiceProvider BuildServices(DatabaseProvider provider, string connectionString, Func<Task> afterTheCommentIsCreated)
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

    var real = _github.Gateway;
    var gateway = A.Fake<IForgeGateway>();
    A.CallTo(() => gateway.GetRepositoryConnectionAsync(A<string>._, A<string>._, A<CancellationToken>._)).Returns(new ForgeConnectionInfo("77", "1001", "4242"));
    A.CallTo(() => gateway.GetRepositoryAsync(A<ForgeRepositoryRef>._, A<CancellationToken>._)).Returns(new ForgeRepositoryInfo("4242", "first-org", string.Empty, "app", "main", true));
    A.CallTo(() => gateway.GetTreeAsync(A<ForgeRepositoryRef>._, A<GitSha>._, A<bool>._, A<CancellationToken>._)).Returns((ForgeTree?)null);
    A.CallTo(() => gateway.GetContentAsync(A<ForgeRepositoryRef>._, A<string>._, A<GitSha>._, A<CancellationToken>._)).Returns((string?)null);
    A.CallTo(() => gateway.ListCommitsAsync(A<ForgeRepositoryRef>._, A<GitSha>._, A<int>._, A<CancellationToken>._)).Returns([]);
    A.CallTo(() => gateway.GetPullAsync(A<ForgeRepositoryRef>._, 12, A<CancellationToken>._))
     .Returns(new ForgePullInfo(12, ForgePullState.Open, "Add a feature", "octocat", Sha, "feature/x", "main", BaseSha));
    A.CallTo(() => gateway.CompareAsync(A<ForgeRepositoryRef>._, A<GitSha>._, A<GitSha>._, A<CancellationToken>._))
     .Returns(new ForgeCompareResult(BaseSha, 1, 0, [], false));
    A.CallTo(() => gateway.ListPullFilesAsync(A<ForgeRepositoryRef>._, A<int>._, A<CancellationToken>._)).Returns(new ForgePullFiles([], false));

    // The comments go through the real gateway, to the WireMock server.
    A.CallTo(() => gateway.ListIssueCommentsAsync(A<ForgeRepositoryRef>._, A<int>._, A<CancellationToken>._))
     .ReturnsLazily((ForgeRepositoryRef repository, int number, CancellationToken token) => real.ListIssueCommentsAsync(repository, number, token));
    A.CallTo(() => gateway.UpdateIssueCommentAsync(A<ForgeRepositoryRef>._, A<long>._, A<string>._, A<CancellationToken>._))
     .ReturnsLazily((ForgeRepositoryRef repository, long id, string body, CancellationToken token) => real.UpdateIssueCommentAsync(repository, id, body, token));
    A.CallTo(() => gateway.CreateIssueCommentAsync(A<ForgeRepositoryRef>._, A<int>._, A<string>._, A<CancellationToken>._))
     .ReturnsLazily(async (ForgeRepositoryRef repository, int number, string body, CancellationToken token) =>
       {
         var created = await real.CreateIssueCommentAsync(repository, number, body, token);

         await afterTheCommentIsCreated();

         return created;
       });

    var gateways = A.Fake<IForgeGatewayProvider>();
    A.CallTo(() => gateways.GetGateway(A<Forge>._)).Returns(gateway);
    services.RemoveAll<IForgeGatewayProvider>();
    services.AddSingleton(gateways);

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

  #region Implementation of IDisposable

  public void Dispose() => _github.Dispose();

  #endregion
}
