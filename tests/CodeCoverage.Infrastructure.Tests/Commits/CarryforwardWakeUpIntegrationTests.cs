// ------------------------------------------------------------------------------------------------
// <copyright file="CarryforwardWakeUpIntegrationTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

using CodeCoverage.Application;
using CodeCoverage.Application.Features.Uploads.Commands.ReceiveUpload;
using CodeCoverage.Domain.Commits;
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

namespace CodeCoverage.Infrastructure.Tests.Commits;

[Collection(DatabaseCollection.Name)]
public sealed class CarryforwardWakeUpIntegrationTests(DatabaseFixture database)
{
  #region Fields

  private const string TokenValue = "cvc_abcdefghijklmnopqrstuvwxyz0123456789ABCDEFG";

  private const string Config = "flags:\n  back:\n    carryforward: true\n";

  private static readonly GitSha OriginSha = GitSha.Parse(new string('1', GitSha.Length));

  private static readonly GitSha AncestorSha = GitSha.Parse(new string('2', GitSha.Length));

  private static readonly GitSha HeadSha = GitSha.Parse(new string('3', GitSha.Length));

  private static readonly DateTimeOffset Now = new(2026, 11, 9, 8, 0, 0, TimeSpan.Zero);

  #endregion

  #region Properties

  private static CancellationToken Ct => TestContext.Current.CancellationToken;

  #endregion

  #region Methods

  // The lost wake-up: the head reads its ancestor pending and passes it, then the ancestor completes and lists the
  // waiting commits before the head saves its AncestorPending state. The head must queue its own finalization again.
  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task Finalize_AncestorCompletesWhileTheHeadPassesIt_ShouldQueueTheHeadAgain(DatabaseProvider provider)
  {
    var seed = await TwoOrganizationDatabase.CreateAsync(database, provider, Ct);
    await SeedTokenAsync(seed);
    var clock = new ManualClock(Now);
    var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    await using var services = BuildServices(provider, seed.ConnectionString, clock, entered, release);
    var fixture = await File.ReadAllBytesAsync(Path.Combine(AppContext.BaseDirectory, "fixtures", "reports", "cobertura", "coverlet-async.xml"), Ct);
    var worker = services.GetServices<IHostedService>().OfType<JobWorker>().Single();

    // The origin completes with the carried flag.
    await SendAsync(services, OriginSha, ["back"], fixture);
    (await worker.RunOnceAsync("worker-1", Ct)).ShouldBeTrue();
    (await worker.RunOnceAsync("worker-1", Ct)).ShouldBeTrue();

    // The head and its ancestor are processed; the finalization of the head is queued first.
    await SendAsync(services, HeadSha, ["front"], fixture);
    await SendAsync(services, AncestorSha, ["back"], fixture);
    (await worker.RunOnceAsync("worker-1", Ct)).ShouldBeTrue();
    (await worker.RunOnceAsync("worker-1", Ct)).ShouldBeTrue();

    var head = await CommitIdAsync(seed, HeadSha);
    clock.Advance(TimeSpan.FromMinutes(11));

    var headRun = worker.RunOnceAsync("worker-1", Ct);
    await entered.Task.WaitAsync(TimeSpan.FromMinutes(1), Ct);

    (await worker.RunOnceAsync("worker-2", Ct)).ShouldBeTrue();
    release.SetResult();
    (await headRun).ShouldBeTrue();

    await using var check = seed.ContextOf(seed.FirstId);
    (await check.Commits.SingleAsync(c => c.Sha == AncestorSha, Ct)).State.ShouldBe(CommitState.Complete);
    (await check.Commits.SingleAsync(c => c.Id == head, Ct)).CarryforwardStatus.ShouldBe(CarryforwardStatus.AncestorPending);

    await using var jobs = seed.ContextWithoutOrganization();
    (await jobs.Jobs.CountAsync(j => j.Type == JobTypes.FinalizeCommit && j.State == JobState.Queued && j.LockKey == $"o{seed.FirstId}:commit:{head}", Ct))
      .ShouldBe(1, "the head must be finalized again from the completed ancestor");
  }

  private static async Task<long> CommitIdAsync(TwoOrganizationDatabase seed, GitSha sha)
  {
    await using var context = seed.ContextOf(seed.FirstId);

    return (await context.Commits.SingleAsync(c => c.Sha == sha, Ct)).Id;
  }

  private static async Task SendAsync(ServiceProvider services, GitSha sha, string[] flags, byte[] report)
  {
    await using var scope = services.CreateAsyncScope();

    var key = sha.Value[..8];
    var metadata = new UploadMetadata(RepositorySlug.Parse("first-org/app"), sha, "main", null, flags, key, null, null, new CiContext(null, null, null, key));
    var credential = new UploadCredential(UploadCredentialKind.UploadToken, TokenValue);

    await scope.ServiceProvider.GetRequiredService<ISender>().Send(new ReceiveUploadCommand(credential, metadata, Parts(report), key), Ct);
  }

  private static async IAsyncEnumerable<UploadPart> Parts(byte[] report)
  {
    await Task.CompletedTask;

    yield return new UploadPart(UploadPartKind.Report, "coverage.cobertura.xml", new MemoryStream(report));
  }

  private static IReadOnlyList<ForgeCommitInfo> History(GitSha from)
  {
    ForgeCommitInfo[] chain =
    [
      new(HeadSha, [AncestorSha], "head", null, Now),
      new(AncestorSha, [OriginSha], "ancestor", null, Now),
      new(OriginSha, [], "origin", null, Now),
    ];

    return [.. chain.SkipWhile(commit => commit.Sha != from)];
  }

  private static ServiceProvider BuildServices(
    DatabaseProvider provider,
    string connectionString,
    ManualClock clock,
    TaskCompletionSource entered,
    TaskCompletionSource release)
  {
    var configuration = new ConfigurationBuilder()
                        .AddInMemoryCollection(new Dictionary<string, string?>
                        {
                          ["ConnectionStrings:CodeCoverage"] = connectionString,
                          ["CodeCoverage:Database:Provider"] = provider.ToString(),
                        })
                        .Build();
    var services = new ServiceCollection().AddSingleton<IConfiguration>(configuration);

    services.AddSingleton<IClock>(clock);
    services.AddLogging().AddApplication().AddInfrastructure().AddPostgreSqlProvider().AddSqlServerProvider().AddJobProcessing();

    var gateway = A.Fake<IForgeGateway>();
    A.CallTo(() => gateway.GetRepositoryConnectionAsync(A<string>._, A<string>._, A<CancellationToken>._)).Returns(new ForgeConnectionInfo("77", "1001", "4242"));
    A.CallTo(() => gateway.GetRepositoryAsync(A<ForgeRepositoryRef>._, A<CancellationToken>._)).Returns(new ForgeRepositoryInfo("4242", "first-org", string.Empty, "app", "main", true));
    A.CallTo(() => gateway.GetTreeAsync(A<ForgeRepositoryRef>._, A<GitSha>._, true, A<CancellationToken>._)).Returns((ForgeTree?)null);
    A.CallTo(() => gateway.GetTreeAsync(A<ForgeRepositoryRef>._, A<GitSha>._, false, A<CancellationToken>._))
     .ReturnsLazily((ForgeRepositoryRef _, GitSha sha, bool _, CancellationToken _) => new ForgeTree(sha, [new ForgeTreeEntry("codecov.yml", sha, false)], false));
    A.CallTo(() => gateway.GetContentAsync(A<ForgeRepositoryRef>._, "codecov.yml", A<GitSha>._, A<CancellationToken>._)).Returns(Config);
    A.CallTo(() => gateway.ListCommitsAsync(A<ForgeRepositoryRef>._, A<GitSha>._, A<int>._, A<CancellationToken>._))
     .ReturnsLazily((ForgeRepositoryRef _, GitSha sha, int _, CancellationToken _) => History(sha));
    A.CallTo(() => gateway.CompareAsync(A<ForgeRepositoryRef>._, A<GitSha>._, A<GitSha>._, A<CancellationToken>._))
     .ReturnsLazily(async (ForgeRepositoryRef _, GitSha baseSha, GitSha headSha, CancellationToken _) =>
       {
         if (headSha == HeadSha)
         {
           entered.TrySetResult();
           await release.Task.ConfigureAwait(false);
         }

         return (ForgeCompareResult?)new ForgeCompareResult(baseSha, 1, 0, [], false);
       });

    var gateways = A.Fake<IForgeGatewayProvider>();
    A.CallTo(() => gateways.GetGateway(A<Forge>._)).Returns(gateway);
    services.RemoveAll<IForgeGatewayProvider>();
    services.AddSingleton(gateways);

    var protector = A.Fake<ISecretProtector>();
    A.CallTo(() => protector.Protect(A<string>._)).Returns("protected");
    services.AddSingleton(protector);

    return services.BuildServiceProvider();
  }

  private static async Task SeedTokenAsync(TwoOrganizationDatabase seed)
  {
    await using var context = seed.ContextOf(seed.FirstId);
    var organization = await context.Organizations.SingleAsync(o => o.Id == seed.FirstId, Ct);

    organization.Admit("root", Now);

    var hash = new Sha256TokenHasher().Hash(TokenValue);

    context.UploadTokens.Add(UploadToken.Create(UploadTokenScope.Organization, null, "ci", "hint", hash, false, "root", Now, null));
    await context.SaveChangesAsync(Ct);
  }

  #endregion
}
