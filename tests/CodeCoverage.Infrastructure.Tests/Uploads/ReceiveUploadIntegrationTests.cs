// ------------------------------------------------------------------------------------------------
// <copyright file="ReceiveUploadIntegrationTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using CodeCoverage.Application;
using CodeCoverage.Application.Features.Uploads.Commands.ReceiveUpload;
using CodeCoverage.Domain.Contracts.Forges;
using CodeCoverage.Domain.Contracts.Jobs;
using CodeCoverage.Domain.Contracts.Uploads;
using CodeCoverage.Domain.Git;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Domain.Repositories;
using CodeCoverage.Domain.Tokens;
using CodeCoverage.Infrastructure.Persistence;
using CodeCoverage.Infrastructure.PostgreSql;
using CodeCoverage.Infrastructure.Security;
using CodeCoverage.Infrastructure.SqlServer;
using CodeCoverage.Infrastructure.Tests.Support;

namespace CodeCoverage.Infrastructure.Tests.Uploads;

[Collection(DatabaseCollection.Name)]
public sealed class ReceiveUploadIntegrationTests(DatabaseFixture database)
{
  #region Fields

  private const int MatrixSize = 6;

  private const string TokenValue = "cvc_abcdefghijklmnopqrstuvwxyz0123456789ABCDEFG";

  private const string AccountId = "1001";

  private const string RepositoryExternalId = "4242";

  private static readonly GitSha Sha = GitSha.Parse(new string('a', GitSha.Length));

  private static readonly DateTimeOffset Now = new(2026, 11, 9, 8, 0, 0, TimeSpan.Zero);

  #endregion

  #region Properties

  private static CancellationToken Ct => TestContext.Current.CancellationToken;

  #endregion

  #region Methods

  // The barrier holds every request after it found neither the repository nor the installation: each one adds both,
  // and every commit starts by an insert, so that the collisions always happen.
  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task Send_ParallelUploadsOfAMatrixOnANewRepository_ShouldEachGetItsSequence(DatabaseProvider provider)
  {
    var seed = await TwoOrganizationDatabase.CreateAsync(database, provider, Ct);
    await SeedTokenAsync(seed);
    var gateway = Gateway(new Barrier(MatrixSize));
    await using var services = BuildServices(provider, seed.ConnectionString, gateway);

    var received = await Task.WhenAll(Enumerable.Range(1, MatrixSize).Select(index => SendAsync(services, $"job-{index}")));

    received.Select(static upload => upload.IsReplay).ShouldAllBe(static isReplay => !isReplay);
    received.Select(static upload => upload.Upload.Sequence).Order().ShouldBe(Enumerable.Range(1, MatrixSize));

    await using var check = seed.ContextOf(seed.FirstId);
    (await check.Repositories.CountAsync(Ct)).ShouldBe(1);
    (await check.Commits.CountAsync(Ct)).ShouldBe(1);
    (await check.Uploads.CountAsync(Ct)).ShouldBe(MatrixSize);
    (await check.Installations.CountAsync(Ct)).ShouldBe(1);
    await using var jobs = seed.ContextWithoutOrganization();
    (await jobs.Jobs.CountAsync(j => j.Type == JobTypes.ProcessUpload, Ct)).ShouldBe(MatrixSize);
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task Send_SameKeyTwice_ShouldQueueOneJobAndKeepTheFirstParts(DatabaseProvider provider)
  {
    var seed = await TwoOrganizationDatabase.CreateAsync(database, provider, Ct);
    await SeedTokenAsync(seed);
    await using var services = BuildServices(provider, seed.ConnectionString, Gateway(null));

    var first = await SendAsync(services, "same");
    var replay = await SendAsync(services, "same");

    replay.IsReplay.ShouldBeTrue();
    replay.Upload.Id.ShouldBe(first.Upload.Id);

    await using var jobs = seed.ContextWithoutOrganization();
    (await jobs.Jobs.CountAsync(Ct)).ShouldBe(1);
    await using var check = seed.ContextOf(seed.FirstId);
    (await check.UploadRawFiles.CountAsync(Ct)).ShouldBe(1);
    (await check.UploadRawFiles.SingleAsync(Ct)).UploadPublicId.ShouldBe(first.Upload.PublicId);
  }

  private static async Task<CodeCoverage.Domain.Uploads.ReceivedUpload> SendAsync(ServiceProvider services, string key)
  {
    await using var scope = services.CreateAsyncScope();

    var metadata = new UploadMetadata(RepositorySlug.Parse("first-org/app"), Sha, "main", null, ["back"], key, null, null, new CiContext(null, null, null, key));
    var credential = new UploadCredential(UploadCredentialKind.UploadToken, TokenValue);
    var command = new ReceiveUploadCommand(credential, metadata, Parts(), key);

    return await scope.ServiceProvider.GetRequiredService<ISender>().Send(command, Ct);
  }

  private static async IAsyncEnumerable<UploadPart> Parts()
  {
    await Task.CompletedTask;

    yield return new UploadPart(UploadPartKind.Report, "coverage.xml", new MemoryStream("<coverage/>"u8.ToArray()));
  }

  private static IForgeGateway Gateway(Barrier? barrier)
  {
    var gateway = A.Fake<IForgeGateway>();

    A.CallTo(() => gateway.GetRepositoryConnectionAsync(A<string>._, A<string>._, A<CancellationToken>._))
     .Returns(new ForgeConnectionInfo("77", AccountId, RepositoryExternalId));

    A.CallTo(() => gateway.GetRepositoryAsync(A<ForgeRepositoryRef>._, A<CancellationToken>._))
     .ReturnsLazily(() =>
       {
         barrier?.SignalAndWait(TimeSpan.FromSeconds(30));

         return Task.FromResult<ForgeRepositoryInfo?>(new ForgeRepositoryInfo(RepositoryExternalId, "first-org", string.Empty, "app", "main", true));
       });

    return gateway;
  }

  private static ServiceProvider BuildServices(DatabaseProvider provider, string connectionString, IForgeGateway gateway)
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
