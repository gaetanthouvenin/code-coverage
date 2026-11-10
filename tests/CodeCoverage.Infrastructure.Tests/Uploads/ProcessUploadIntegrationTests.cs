// ------------------------------------------------------------------------------------------------
// <copyright file="ProcessUploadIntegrationTests.cs" company="Gaëtan Thouvenin">
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
using CodeCoverage.Domain.Coverage;
using CodeCoverage.Domain.Git;
using CodeCoverage.Domain.Operations;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Domain.Repositories;
using CodeCoverage.Domain.Tokens;
using CodeCoverage.Infrastructure.Identity;
using CodeCoverage.Infrastructure.Jobs;
using CodeCoverage.Infrastructure.Persistence;
using CodeCoverage.Infrastructure.PostgreSql;
using CodeCoverage.Infrastructure.Security;
using CodeCoverage.Infrastructure.SqlServer;
using CodeCoverage.Infrastructure.Tests.Support;

namespace CodeCoverage.Infrastructure.Tests.Uploads;

[Collection(DatabaseCollection.Name)]
public sealed class ProcessUploadIntegrationTests(DatabaseFixture database)
{
  #region Fields

  private const string TokenValue = "cvc_abcdefghijklmnopqrstuvwxyz0123456789ABCDEFG";

  private const string WorkerId = "worker-1";

  private static readonly GitSha Sha = GitSha.Parse(new string('a', GitSha.Length));

  private static readonly DateTimeOffset Now = new(2026, 11, 9, 8, 0, 0, TimeSpan.Zero);

  #endregion

  #region Properties

  private static CancellationToken Ct => TestContext.Current.CancellationToken;

  #endregion

  #region Methods

  // From the reception to the stored coverage, through the real parser and the real job: only the forge is faked.
  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task Worker_CoverletReport_ShouldStoreItsCoverageAndQueueTheFinalization(DatabaseProvider provider)
  {
    var seed = await TwoOrganizationDatabase.CreateAsync(database, provider, Ct);
    await SeedTokenAsync(seed);
    await using var services = BuildServices(provider, seed.ConnectionString);
    var fixture = Path.Combine(AppContext.BaseDirectory, "fixtures", "reports", "cobertura", "coverlet-async.xml");
    var expected = await ParseAsync(services, fixture);

    var received = await SendAsync(services, await File.ReadAllBytesAsync(fixture, Ct));

    (await Worker(services).RunOnceAsync(WorkerId, Ct)).ShouldBeTrue();

    await using var check = seed.ContextOf(seed.FirstId);
    var upload = await check.Uploads.SingleAsync(u => u.PublicId == received, Ct);

    upload.State.ShouldBe(UploadState.Processed);
    upload.ReportFileCount.ShouldBe(1);
    upload.ResolvedFileCount.ShouldBe(expected.Files.Count);
    upload.Lines.ShouldBe(expected.Totals.Lines);
    upload.Hits.ShouldBe(expected.Totals.Hits);
    upload.ProcessingLog.Messages.ShouldHaveSingleItem().ShouldContain("not verified");
    (await check.UploadCoverages.CountAsync(c => c.UploadId == upload.Id, Ct)).ShouldBe(1);

    await using var jobs = seed.ContextWithoutOrganization();
    var finalize = await jobs.Jobs.SingleAsync(j => j.Type == JobTypes.FinalizeCommit, Ct);
    finalize.OrganizationId.ShouldBe(seed.FirstId);
    finalize.LockKey.ShouldBe($"o{seed.FirstId}:commit:{upload.CommitId}");
  }

  // Each upload writes its own row: the two jobs run at the same time without touching one another.
  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task Worker_TwoUploadsOfACommit_ShouldBothEndProcessed(DatabaseProvider provider)
  {
    var seed = await TwoOrganizationDatabase.CreateAsync(database, provider, Ct);
    await SeedTokenAsync(seed);
    await using var services = BuildServices(provider, seed.ConnectionString);
    var fixture = await File.ReadAllBytesAsync(Path.Combine(AppContext.BaseDirectory, "fixtures", "reports", "cobertura", "coverlet-async.xml"), Ct);

    await SendAsync(services, fixture, "first");
    await SendAsync(services, fixture, "second");

    var worker = Worker(services);
    var ran = await Task.WhenAll(worker.RunOnceAsync("worker-1", Ct), worker.RunOnceAsync("worker-2", Ct));

    ran.ShouldAllBe(static value => value);

    await using var check = seed.ContextOf(seed.FirstId);
    (await check.Uploads.CountAsync(static u => u.State == UploadState.Processed, Ct)).ShouldBe(2);
    (await check.UploadCoverages.CountAsync(Ct)).ShouldBe(2);

    await using var jobs = seed.ContextWithoutOrganization();
    (await jobs.Jobs.CountAsync(j => j.Type == JobTypes.FinalizeCommit && j.State == JobState.Queued, Ct)).ShouldBe(1, "the finalizations of a commit are merged");
  }

  // The first upload processed end to end: reception, parsing, finalization.
  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task Worker_UploadThenFinalization_ShouldCompleteTheCommit(DatabaseProvider provider)
  {
    var seed = await TwoOrganizationDatabase.CreateAsync(database, provider, Ct);
    await SeedTokenAsync(seed);
    await using var services = BuildServices(provider, seed.ConnectionString);
    var fixture = await File.ReadAllBytesAsync(Path.Combine(AppContext.BaseDirectory, "fixtures", "reports", "cobertura", "coverlet-async.xml"), Ct);

    await SendAsync(services, fixture);

    var worker = Worker(services);

    (await worker.RunOnceAsync(WorkerId, Ct)).ShouldBeTrue();
    (await worker.RunOnceAsync(WorkerId, Ct)).ShouldBeTrue();

    await using var check = seed.ContextOf(seed.FirstId);
    var commit = await check.Commits.SingleAsync(Ct);

    commit.State.ShouldBe(CommitState.Complete);
    commit.ReportRevision.ShouldBe(1);
    commit.SessionCount.ShouldBe(1);
    commit.Lines.ShouldBeGreaterThan(0);
    (await check.CommitSessions.CountAsync(Ct)).ShouldBe(1);
    (await check.CommitFlagTotals.SingleAsync(Ct)).Flag.ShouldBe(CommitFlagTotals.ProjectFlag);
  }

  // The acceptance of the reprocessing: with the same parsers, every upload is parsed again from its raw files and the
  // commit is rebuilt byte for byte, in a new revision. Two requests that wait give one reprocessing.
  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task Worker_ReprocessedCommit_ShouldRebuildTheSameReport(DatabaseProvider provider)
  {
    var seed = await TwoOrganizationDatabase.CreateAsync(database, provider, Ct);
    await SeedTokenAsync(seed);
    await using var services = BuildServices(provider, seed.ConnectionString);
    var cobertura = await File.ReadAllBytesAsync(Path.Combine(AppContext.BaseDirectory, "fixtures", "reports", "cobertura", "coverlet-async.xml"), Ct);
    var lcov = await File.ReadAllBytesAsync(Path.Combine(AppContext.BaseDirectory, "fixtures", "reports", "lcov", "vitest-relative.info"), Ct);

    await SendAsync(services, cobertura, "back", ["back"]);
    await SendAsync(services, lcov, "front", ["front"], "lcov.info");

    var worker = Worker(services);
    await RunUntilIdleAsync(worker);

    var before = await SnapshotAsync(seed);

    before.Commit.State.ShouldBe(CommitState.Complete);
    before.Commit.ReportRevision.ShouldBe(1);
    before.Uploads.ShouldAllBe(static upload => upload.State == UploadState.Processed);
    before.FlagTotals.Select(static totals => totals.Flag).ShouldBe([CommitFlagTotals.ProjectFlag, "back", "front"], ignoreOrder: true);

    await QueueReprocessAsync(services, seed, before.Commit.Id);
    await QueueReprocessAsync(services, seed, before.Commit.Id);

    await using (var jobs = seed.ContextWithoutOrganization())
    {
      var reprocess = await jobs.Jobs.AsNoTracking().SingleAsync(static j => j.Type == JobTypes.ReprocessCommit, Ct);

      reprocess.State.ShouldBe(JobState.Queued);
      reprocess.OrganizationId.ShouldBe(seed.FirstId);
      reprocess.LockKey.ShouldBe($"o{seed.FirstId}:commit:{before.Commit.Id}", "the lock of the finalization of the commit");
      reprocess.DedupKey.ShouldBe($"o{seed.FirstId}:reprocess:{before.Commit.Id}");
    }

    // The job of the reprocessing alone: every upload is back in the queue, and the report is not touched yet.
    (await worker.RunOnceAsync(WorkerId, Ct)).ShouldBeTrue();

    var during = await SnapshotAsync(seed);

    during.Uploads.ShouldAllBe(static upload => upload.State == UploadState.Received && upload.ProcessedAt == null && upload.Lines == 0);
    during.Report.ShouldBe(before.Report);
    during.Commit.ReportRevision.ShouldBe(1);

    await using (var jobs = seed.ContextWithoutOrganization())
    {
      (await jobs.Jobs.CountAsync(static j => j.Type == JobTypes.ProcessUpload && j.State == JobState.Queued, Ct)).ShouldBe(2);
    }

    await RunUntilIdleAsync(worker);

    var after = await SnapshotAsync(seed);

    after.Commit.State.ShouldBe(CommitState.Complete);
    after.Commit.ReportRevision.ShouldBe(2, "the two uploads end before the one finalization they queue");
    after.Report.ShouldBe(before.Report);
    after.FileIndex.ShouldBe(before.FileIndex);
    after.Coverages.ShouldBe(before.Coverages);
    after.Sessions.ShouldBe(before.Sessions);
    after.FlagTotals.Select(Describe).ShouldBe(before.FlagTotals.Select(Describe));
    (after.Commit.Lines, after.Commit.Hits, after.Commit.Misses, after.Commit.Partials, after.Commit.Files, after.Commit.SessionCount)
      .ShouldBe((before.Commit.Lines, before.Commit.Hits, before.Commit.Misses, before.Commit.Partials, before.Commit.Files, before.Commit.SessionCount));
    after.Uploads.Select(Describe).ShouldBe(before.Uploads.Select(Describe));

    await using var ended = seed.ContextWithoutOrganization();
    (await ended.Jobs.CountAsync(static j => j.State != JobState.Succeeded, Ct)).ShouldBe(0);
  }

  // The raw files went past their retention between the request and the job: the job fails for good and no upload is touched.
  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task Worker_ReprocessWithExpiredRaw_ShouldFailTheJobAndKeepTheCommit(DatabaseProvider provider)
  {
    var seed = await TwoOrganizationDatabase.CreateAsync(database, provider, Ct);
    await SeedTokenAsync(seed);
    await using var services = BuildServices(provider, seed.ConnectionString);
    var cobertura = await File.ReadAllBytesAsync(Path.Combine(AppContext.BaseDirectory, "fixtures", "reports", "cobertura", "coverlet-async.xml"), Ct);

    await SendAsync(services, cobertura);

    var worker = Worker(services);
    await RunUntilIdleAsync(worker);

    var before = await SnapshotAsync(seed);

    await using (var raw = seed.ContextOf(seed.FirstId))
    {
      await raw.UploadRawFiles.ExecuteUpdateAsync(static s => s.SetProperty(static f => f.ExpiresAt, DateTimeOffset.UnixEpoch), Ct);
    }

    await QueueReprocessAsync(services, seed, before.Commit.Id);
    await RunUntilIdleAsync(worker);

    var after = await SnapshotAsync(seed);

    after.Uploads.ShouldAllBe(static upload => upload.State == UploadState.Processed);
    after.Report.ShouldBe(before.Report);
    after.Commit.ReportRevision.ShouldBe(1);

    await using var jobs = seed.ContextWithoutOrganization();
    var reprocess = await jobs.Jobs.AsNoTracking().SingleAsync(static j => j.Type == JobTypes.ReprocessCommit, Ct);

    reprocess.State.ShouldBe(JobState.Failed, "a refusal that no retry can lift");
    reprocess.LastError.ShouldNotBeNull().ShouldContain("Commit.RawExpired");
    (await jobs.Jobs.CountAsync(static j => j.Type == JobTypes.ProcessUpload && j.State == JobState.Queued, Ct)).ShouldBe(0);
  }

  private static string Describe(CommitFlagTotals totals) =>
    $"{totals.Flag}|{totals.Lines}|{totals.Hits}|{totals.Misses}|{totals.Partials}|{totals.Coverage}|{totals.IsCarriedForward}";

  private static string Describe(Upload upload) =>
    string.Join(
      '|',
      upload.Sequence,
      upload.State,
      upload.ReportFileCount,
      upload.ResolvedFileCount,
      upload.UnresolvedFileCount,
      upload.Lines,
      upload.Hits,
      upload.Misses,
      upload.Partials,
      upload.ErrorCode,
      string.Join(';', upload.ProcessingLog.Messages),
      string.Join(';', upload.ProcessingLog.Paths)
    );

  private static async Task RunUntilIdleAsync(JobWorker worker)
  {
    for (var round = 0; round < 50; round++)
    {
      if (!await worker.RunOnceAsync(WorkerId, Ct))
      {
        return;
      }
    }

    throw new InvalidOperationException("The queue never emptied.");
  }

  // What an administrator of the repository triggers: the job of the reprocessing, queued in the organization of the commit.
  private static async Task QueueReprocessAsync(ServiceProvider services, TwoOrganizationDatabase seed, long commitId)
  {
    await using var scope = services.CreateAsyncScope();

    scope.ServiceProvider.GetRequiredService<OrganizationContext>().Enter(seed.FirstId);

    await scope.ServiceProvider.GetRequiredService<IJobQueue>().EnqueueAsync(ReprocessCommitJobs.Create(commitId), Ct);
  }

  private static async Task<CommitSnapshot> SnapshotAsync(TwoOrganizationDatabase seed)
  {
    await using var context = seed.ContextOf(seed.FirstId);
    var commit = await context.Commits.AsNoTracking().SingleAsync(Ct);
    var report = await context.CommitReports.AsNoTracking().SingleAsync(Ct);
    var uploads = await context.Uploads.AsNoTracking().OrderBy(static u => u.Sequence).ToListAsync(Ct);
    var coverages = await context.UploadCoverages.AsNoTracking().OrderBy(static c => c.UploadId).ToListAsync(Ct);
    var sessions = await context.CommitSessions.AsNoTracking().OrderBy(static s => s.Number).ToListAsync(Ct);
    var flagTotals = await context.CommitFlagTotals.AsNoTracking().OrderBy(static t => t.Flag).ToListAsync(Ct);

    return new CommitSnapshot(
      commit,
      Convert.ToHexString(report.Data),
      Convert.ToHexString(report.FileIndex),
      uploads,
      [.. coverages.Select(static c => $"{c.UploadId}:{Convert.ToHexString(c.Data)}")],
      [.. sessions.Select(static s => $"{s.Number}|{s.Kind}|{s.UploadId}|{string.Join(',', s.Flags)}|{s.Lines}|{s.Hits}|{s.Misses}|{s.Partials}|{s.Coverage}|{Convert.ToHexString(s.Data ?? [])}")],
      flagTotals
    );
  }

  private static JobWorker Worker(IServiceProvider services) => services.GetServices<IHostedService>().OfType<JobWorker>().Single();

  private static async Task<CoverageReport> ParseAsync(IServiceProvider services, string path)
  {
    await using var stream = File.OpenRead(path);
    var parser = services.GetRequiredService<IReportParserCatalog>().Find(ReportFormat.Cobertura).ShouldNotBeNull();
    var parsed = await parser.ParseAsync(stream, Ct);

    return CoverageReport.Create(parsed.Files.Select(static file => FileCoverage.Create(PathNamed(file.RawPath), file.Lines)));
  }

  private static string PathNamed(string raw) => raw.TrimStart('/');

  private static async Task<Guid> SendAsync(
    ServiceProvider services,
    byte[] report,
    string key = "only",
    IReadOnlyList<string>? flags = null,
    string fileName = "coverage.cobertura.xml")
  {
    await using var scope = services.CreateAsyncScope();

    var metadata = new UploadMetadata(RepositorySlug.Parse("first-org/app"), Sha, "main", null, flags ?? [], key, null, null, new CiContext(null, null, null, key));
    var credential = new UploadCredential(UploadCredentialKind.UploadToken, TokenValue);
    var command = new ReceiveUploadCommand(credential, metadata, Parts(report, fileName), key);
    var received = await scope.ServiceProvider.GetRequiredService<ISender>().Send(command, Ct);

    return received.Upload.PublicId;
  }

  private static async IAsyncEnumerable<UploadPart> Parts(byte[] report, string fileName)
  {
    await Task.CompletedTask;

    yield return new UploadPart(UploadPartKind.Report, fileName, new MemoryStream(report));
  }

  private static ServiceProvider BuildServices(DatabaseProvider provider, string connectionString)
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
    A.CallTo(() => gateway.GetTreeAsync(A<ForgeRepositoryRef>._, A<GitSha>._, A<bool>._, A<CancellationToken>._)).Returns((ForgeTree?)null);
    A.CallTo(() => gateway.GetContentAsync(A<ForgeRepositoryRef>._, A<string>._, A<GitSha>._, A<CancellationToken>._)).Returns((string?)null);

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
