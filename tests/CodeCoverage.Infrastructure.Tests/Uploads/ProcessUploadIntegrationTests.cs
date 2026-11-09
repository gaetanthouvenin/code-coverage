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

  private static JobWorker Worker(IServiceProvider services) => services.GetServices<IHostedService>().OfType<JobWorker>().Single();

  private static async Task<CoverageReport> ParseAsync(IServiceProvider services, string path)
  {
    await using var stream = File.OpenRead(path);
    var parser = services.GetRequiredService<IReportParserCatalog>().Find(ReportFormat.Cobertura).ShouldNotBeNull();
    var parsed = await parser.ParseAsync(stream, Ct);

    return CoverageReport.Create(parsed.Files.Select(static file => FileCoverage.Create(PathNamed(file.RawPath), file.Lines)));
  }

  private static string PathNamed(string raw) => raw.TrimStart('/');

  private static async Task<Guid> SendAsync(ServiceProvider services, byte[] report, string key = "only")
  {
    await using var scope = services.CreateAsyncScope();

    var metadata = new UploadMetadata(RepositorySlug.Parse("first-org/app"), Sha, "main", null, [], key, null, null, new CiContext(null, null, null, key));
    var credential = new UploadCredential(UploadCredentialKind.UploadToken, TokenValue);
    var command = new ReceiveUploadCommand(credential, metadata, Parts(report), key);
    var received = await scope.ServiceProvider.GetRequiredService<ISender>().Send(command, Ct);

    return received.Upload.PublicId;
  }

  private static async IAsyncEnumerable<UploadPart> Parts(byte[] report)
  {
    await Task.CompletedTask;

    yield return new UploadPart(UploadPartKind.Report, "coverage.cobertura.xml", new MemoryStream(report));
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
