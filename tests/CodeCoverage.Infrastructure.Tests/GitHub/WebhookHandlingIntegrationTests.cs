// ------------------------------------------------------------------------------------------------
// <copyright file="WebhookHandlingIntegrationTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Text;

using Mediator;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

using CodeCoverage.Application;
using CodeCoverage.Application.Features.GitHubEvents.Commands.ReceiveWebhook;
using CodeCoverage.Domain.Contracts.Forges;
using CodeCoverage.Domain.Contracts.GitHub;
using CodeCoverage.Domain.Contracts.Jobs;
using CodeCoverage.Domain.Operations;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Domain.Tokens;
using CodeCoverage.Infrastructure.Jobs;
using CodeCoverage.Infrastructure.Persistence;
using CodeCoverage.Infrastructure.PostgreSql;
using CodeCoverage.Infrastructure.SqlServer;
using CodeCoverage.Infrastructure.Tests.Support;

using static CodeCoverage.Infrastructure.Tests.Support.RepositorySeeder;

namespace CodeCoverage.Infrastructure.Tests.GitHub;

// From the reception of a delivery to its effects, through the real queue, the real worker and the real handlers:
// only GitHub and the signature are faked.
[Collection(DatabaseCollection.Name)]
public sealed class WebhookHandlingIntegrationTests(DatabaseFixture database)
{
  #region Fields

  private const string WorkerId = "worker-1";

  private const string ExternalId = "4242";

  private readonly IGitHubAppGateway _gitHub = A.Fake<IGitHubAppGateway>();

  private readonly IForgeGateway _forge = A.Fake<IForgeGateway>();

  #endregion

  #region Properties

  private static CancellationToken Ct => TestContext.Current.CancellationToken;

  #endregion

  #region Methods

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task Worker_StoredRepositoryRename_ShouldRenameInsideItsOrganizationOnly(DatabaseProvider provider)
  {
    var seed = await SeededAsync(provider, admitSecond: true);
    await AddRepositoryAsync(seed, seed.FirstId, FirstInstallation, "first-org/old-name");
    await AddRepositoryAsync(seed, seed.SecondId, SecondInstallation, "second-org/old-name");
    await using var services = BuildServices(provider, seed.ConnectionString);

    var outcome = await ReceiveAsync(services, "d-1", "repository", RepositoryBody("renamed", FirstInstallation, "first-org", 1001, "new-name"));
    await RunUntilIdleAsync(services);

    outcome.ShouldBe(WebhookOutcome.Accepted);

    (await NameAsync(seed, seed.FirstId)).ShouldBe("new-name");
    (await NameAsync(seed, seed.SecondId)).ShouldBe("old-name");

    await using var check = seed.ContextWithoutOrganization();
    (await check.WebhookDeliveries.AsNoTracking().SingleAsync(Ct)).ProcessedAt.ShouldNotBeNull();
    (await check.Jobs.AsNoTracking().SingleAsync(Ct)).State.ShouldBe(JobState.Succeeded);
  }

  // The acceptance of M2.03: a delivery GitHub sends again is absorbed.
  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task Worker_RedeliveredRename_ShouldBeAbsorbed(DatabaseProvider provider)
  {
    var seed = await SeededAsync(provider, admitSecond: false);
    await AddRepositoryAsync(seed, seed.FirstId, FirstInstallation, "first-org/old-name");
    await using var services = BuildServices(provider, seed.ConnectionString);
    var body = RepositoryBody("renamed", FirstInstallation, "first-org", 1001, "new-name");

    (await ReceiveAsync(services, "d-1", "repository", body)).ShouldBe(WebhookOutcome.Accepted);
    await RunUntilIdleAsync(services);
    var version = await VersionAsync(seed, seed.FirstId);

    (await ReceiveAsync(services, "d-1", "repository", body)).ShouldBe(WebhookOutcome.Duplicate);
    await RunUntilIdleAsync(services);

    (await VersionAsync(seed, seed.FirstId)).ShouldBe(version);

    await using var check = seed.ContextWithoutOrganization();
    (await check.Jobs.AsNoTracking().CountAsync(Ct)).ShouldBe(1);
  }

  // No data moves: the recipient gets its own row, and the origin closes its row, revokes its tokens and audits it.
  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task Worker_Transfer_ShouldCreateTheRowOfTheRecipientAndCloseTheOneOfTheOrigin(DatabaseProvider provider)
  {
    var seed = await SeededAsync(provider, admitSecond: true);
    var originRepository = await AddRepositoryAsync(seed, seed.FirstId, FirstInstallation, "first-org/app");
    var repositoryToken = await AddTokenAsync(seed, seed.FirstId, UploadTokenScope.Repository, originRepository, 1);
    var organizationToken = await AddTokenAsync(seed, seed.FirstId, UploadTokenScope.Organization, null, 2);
    var recipientToken = await AddTokenAsync(seed, seed.SecondId, UploadTokenScope.Organization, null, 3);
    await using var services = BuildServices(provider, seed.ConnectionString);

    // What the installation of the origin sees once the repository left: a public repository of another account.
    A.CallTo(() => _forge.GetRepositoryAsync(A<ForgeRepositoryRef>._, A<CancellationToken>._))
     .Returns(new ForgeRepositoryInfo(ExternalId, "second-org", string.Empty, "app", "main", false));

    var body = RepositoryBody("transferred", SecondInstallation, "second-org", 2002, "app", previousOwnerId: 1001);
    await ReceiveAsync(services, "d-1", "repository", body);
    await RunUntilIdleAsync(services);

    await using var second = seed.ContextOf(seed.SecondId);
    var received = await second.Repositories.AsNoTracking().SingleAsync(Ct);
    received.ExternalId.ShouldBe(ExternalId);
    received.IsActive.ShouldBeTrue();
    received.InstallationId.ShouldBe(SecondInstallation);
    received.Id.ShouldNotBe(originRepository);
    (await second.UploadTokens.AsNoTracking().SingleAsync(t => t.Id == recipientToken, Ct)).RotationAdvisedAt.ShouldBeNull();

    await using var first = seed.ContextOf(seed.FirstId);
    var closed = await first.Repositories.AsNoTracking().SingleAsync(Ct);
    closed.Id.ShouldBe(originRepository);
    closed.IsActive.ShouldBeFalse();
    closed.BadgeTokenProtected.ShouldNotBe("protected");
    (await first.UploadTokens.AsNoTracking().SingleAsync(t => t.Id == repositoryToken, Ct)).RevokedAt.ShouldNotBeNull();
    var advised = await first.UploadTokens.AsNoTracking().SingleAsync(t => t.Id == organizationToken, Ct);
    advised.RevokedAt.ShouldBeNull();
    advised.RotationAdvisedAt.ShouldNotBeNull();

    await using var check = seed.ContextWithoutOrganization();
    var audit = await check.AuditEntries
                           .AsNoTracking()
                           .Select(static a => new { a.Action, OrganizationId = EF.Property<long?>(a, "OrganizationId") })
                           .ToListAsync(Ct);
    audit.Select(static a => a.Action)
         .ShouldBe(
           ["Repositories.Transfer.RevokeUploadTokens", "Repositories.Transfer.RotateBadgeToken", "Repositories.Transfer.AdviseTokenRotation"],
           ignoreOrder: true
         );
    audit.ShouldAllBe(a => a.OrganizationId == seed.FirstId);

    var jobs = await check.Jobs.AsNoTracking().OrderBy(static j => j.Id).ToListAsync(Ct);
    jobs.Select(static j => j.Type).ShouldBe([JobTypes.HandleWebhook, JobTypes.SyncInstallations, JobTypes.ReconcileRepositoryTransfer]);
    jobs.Select(static j => j.OrganizationId).ShouldBe([seed.SecondId, null, seed.FirstId]);
    jobs.ShouldAllBe(static j => j.State == JobState.Succeeded);
  }

  // The installation of the origin stops listing the repository before the transfer reaches the origin: the row is only
  // deactivated, and no token is touched. The transfer still closes it, with no token of the repository to revoke:
  // the badge token is replaced, the tokens of the organization are marked, and both are audited.
  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task Worker_TransferOfARowAlreadyDeactivated_ShouldStillCloseIt(DatabaseProvider provider)
  {
    var seed = await SeededAsync(provider, admitSecond: true);
    var originRepository = await AddRepositoryAsync(seed, seed.FirstId, FirstInstallation, "first-org/app");
    var organizationToken = await AddTokenAsync(seed, seed.FirstId, UploadTokenScope.Organization, null, 2);
    await using var services = BuildServices(provider, seed.ConnectionString);

    A.CallTo(() => _gitHub.ListInstallationRepositoriesAsync(FirstInstallation, A<CancellationToken>._)).Returns([]);

    await ReceiveAsync(services, "d-1", "installation_repositories", "{\"action\":\"removed\",\"installation\":{\"id\":" + Text(FirstInstallation) + "}}");
    await RunUntilIdleAsync(services);

    string badgeToken;

    await using (var before = seed.ContextOf(seed.FirstId))
    {
      var deactivated = await before.Repositories.AsNoTracking().SingleAsync(Ct);
      deactivated.IsActive.ShouldBeFalse();
      deactivated.TransferredAt.ShouldBeNull();
      badgeToken = deactivated.BadgeTokenProtected;
      (await before.UploadTokens.AsNoTracking().SingleAsync(Ct)).RotationAdvisedAt.ShouldBeNull();
    }

    A.CallTo(() => _forge.GetRepositoryAsync(A<ForgeRepositoryRef>._, A<CancellationToken>._))
     .Returns(new ForgeRepositoryInfo(ExternalId, "second-org", string.Empty, "app", "main", false));

    await ReceiveAsync(services, "d-2", "repository", RepositoryBody("transferred", SecondInstallation, "second-org", 2002, "app", previousOwnerId: 1001));
    await RunUntilIdleAsync(services);

    await using var first = seed.ContextOf(seed.FirstId);
    var closed = await first.Repositories.AsNoTracking().SingleAsync(Ct);
    closed.Id.ShouldBe(originRepository);
    closed.IsActive.ShouldBeFalse();
    closed.TransferredAt.ShouldNotBeNull();
    closed.BadgeTokenProtected.ShouldNotBe(badgeToken);
    var advised = await first.UploadTokens.AsNoTracking().SingleAsync(t => t.Id == organizationToken, Ct);
    advised.RevokedAt.ShouldBeNull();
    advised.RotationAdvisedAt.ShouldNotBeNull();

    await using var check = seed.ContextWithoutOrganization();
    var audit = await check.AuditEntries
                           .AsNoTracking()
                           .Select(static a => new { a.Action, OrganizationId = EF.Property<long?>(a, "OrganizationId") })
                           .ToListAsync(Ct);
    audit.Select(static a => a.Action).ShouldBe(["Repositories.Transfer.RotateBadgeToken", "Repositories.Transfer.AdviseTokenRotation"], ignoreOrder: true);
    audit.ShouldAllBe(a => a.OrganizationId == seed.FirstId);
    (await check.Jobs.AsNoTracking().ToListAsync(Ct)).ShouldAllBe(static j => j.State == JobState.Succeeded);
  }

  // An organization that is not active creates no repository: its event only reaches the coordinator, which skips it.
  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task Worker_InstallationEventOfAPendingOrganization_ShouldCreateNoRepositoryForIt(DatabaseProvider provider)
  {
    var seed = await SeededAsync(provider, admitSecond: false);
    await using var services = BuildServices(provider, seed.ConnectionString);

    A.CallTo(() => _gitHub.ListInstallationsAsync(A<CancellationToken>._))
     .Returns([new GitHubInstallationInfo(FirstInstallation, "1001", "first-org", false), new GitHubInstallationInfo(SecondInstallation, "2002", "second-org", false)]);
    A.CallTo(() => _gitHub.ListInstallationRepositoriesAsync(A<long>._, A<CancellationToken>._))
     .Returns([new ForgeRepositoryInfo(ExternalId, "any-org", string.Empty, "app", "main", true)]);

    var outcome = await ReceiveAsync(services, "d-1", "installation_repositories", $$$"""{"action":"added","installation":{"id":{{{SecondInstallation}}}},"repositories_added":[{"id":4242}]}""");
    await RunUntilIdleAsync(services);

    outcome.ShouldBe(WebhookOutcome.Accepted);

    await using var second = seed.ContextOf(seed.SecondId);
    (await second.Repositories.AsNoTracking().CountAsync(Ct)).ShouldBe(0);

    // The active organization is reconciled by the same coordinator, with its own installation only.
    await using var first = seed.ContextOf(seed.FirstId);
    (await first.Repositories.AsNoTracking().SingleAsync(Ct)).InstallationId.ShouldBe(FirstInstallation);
    A.CallTo(() => _gitHub.ListInstallationRepositoriesAsync(SecondInstallation, A<CancellationToken>._)).MustNotHaveHappened();

    await using var check = seed.ContextWithoutOrganization();
    var jobs = await check.Jobs.AsNoTracking().OrderBy(static j => j.Id).ToListAsync(Ct);
    jobs.Select(static j => j.Type).ShouldBe([JobTypes.HandleWebhook, JobTypes.SyncInstallations, JobTypes.SyncInstallationRepositories]);
    jobs.Select(static j => j.OrganizationId).ShouldBe([null, null, seed.FirstId]);
  }

  // The reconciliation of an installation: tracked, renamed and deactivated in one run, inside its organization.
  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task Worker_InstallationRepositoriesChanged_ShouldUpsertAndDeactivateMissing(DatabaseProvider provider)
  {
    var seed = await SeededAsync(provider, admitSecond: true);
    await AddRepositoryAsync(seed, seed.FirstId, FirstInstallation, "first-org/gone");
    await AddRepositoryAsync(seed, seed.SecondId, SecondInstallation, "second-org/kept");
    await using var services = BuildServices(provider, seed.ConnectionString);

    A.CallTo(() => _gitHub.ListInstallationRepositoriesAsync(FirstInstallation, A<CancellationToken>._))
     .Returns([new ForgeRepositoryInfo("9001", "first-org", string.Empty, "fresh", "main", false)]);

    await ReceiveAsync(services, "d-1", "installation_repositories", $$$"""{"action":"added","installation":{"id":{{{FirstInstallation}}}}}""");
    await RunUntilIdleAsync(services);

    await using var first = seed.ContextOf(seed.FirstId);
    var rows = await first.Repositories.AsNoTracking().OrderBy(static r => r.Name).ToListAsync(Ct);
    rows.Select(static r => (r.Name, r.IsActive)).ShouldBe([("fresh", true), ("gone", false)]);

    await using var second = seed.ContextOf(seed.SecondId);
    (await second.Repositories.AsNoTracking().SingleAsync(Ct)).IsActive.ShouldBeTrue();
  }

  // A repository deleted, then created again under the same name: the old row keeps its history under a
  // released name, and the new repository is tracked instead of failing every reconciliation.
  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task Worker_RepositoryDeletedThenCreatedAgainUnderTheSameName_ShouldTrackTheNewOne(DatabaseProvider provider)
  {
    var seed = await SeededAsync(provider, admitSecond: false);
    var deleted = await AddRepositoryAsync(seed, seed.FirstId, FirstInstallation, "first-org/app");
    await using var services = BuildServices(provider, seed.ConnectionString);

    await ReceiveAsync(services, "d-1", "repository", RepositoryBody("deleted", FirstInstallation, "first-org", 1001, "app"));
    await RunUntilIdleAsync(services);

    A.CallTo(() => _gitHub.ListInstallationRepositoriesAsync(FirstInstallation, A<CancellationToken>._))
     .Returns([new ForgeRepositoryInfo("9001", "first-org", string.Empty, "app", "main", false, "1001")]);

    await ReceiveAsync(services, "d-2", "installation_repositories", "{\"action\":\"added\",\"installation\":{\"id\":" + Text(FirstInstallation) + "}}");
    await RunUntilIdleAsync(services);

    await using var first = seed.ContextOf(seed.FirstId);
    var rows = await first.Repositories.AsNoTracking().OrderBy(static r => r.Id).ToListAsync(Ct);
    rows.Select(static r => (r.ExternalId, r.Name, r.IsActive)).ShouldBe([(ExternalId, "app~4242", false), ("9001", "app", true)]);
    rows[0].Id.ShouldBe(deleted);

    await using var check = seed.ContextWithoutOrganization();
    (await check.Jobs.AsNoTracking().ToListAsync(Ct)).ShouldAllBe(static j => j.State == JobState.Succeeded);
  }

  private static string RepositoryBody(string action, long installationId, string owner, long ownerId, string name, long? previousOwnerId = null)
  {
    var changes = previousOwnerId is { } previous ? ",\"changes\":{\"owner\":{\"from\":{\"organization\":{\"id\":" + Text(previous) + "}}}}" : string.Empty;

    return "{\"action\":\"" + action + "\",\"installation\":{\"id\":" + Text(installationId) + "}" + changes
           + ",\"repository\":{\"id\":" + ExternalId + ",\"name\":\"" + name + "\",\"private\":false,\"default_branch\":\"main\",\"owner\":{\"login\":\"" + owner + "\",\"id\":" + Text(ownerId) + "}}}";
  }

  private static string Text(long value) => value.ToString(System.Globalization.CultureInfo.InvariantCulture);

  private static async Task<WebhookOutcome> ReceiveAsync(ServiceProvider services, string deliveryId, string @event, string body)
  {
    await using var scope = services.CreateAsyncScope();

    return await scope.ServiceProvider.GetRequiredService<ISender>().Send(new ReceiveWebhookCommand(deliveryId, @event, "sha256=test", Encoding.UTF8.GetBytes(body)), Ct);
  }

  private static async Task RunUntilIdleAsync(ServiceProvider services)
  {
    var worker = services.GetServices<IHostedService>().OfType<JobWorker>().Single();

    for (var round = 0; round < 50; round++)
    {
      if (!await worker.RunOnceAsync(WorkerId, Ct))
      {
        return;
      }
    }

    throw new InvalidOperationException("The queue never emptied.");
  }

  private static async Task<long> AddRepositoryAsync(TwoOrganizationDatabase seed, long organizationId, long installationId, string slug)
  {
    await using var context = seed.ContextOf(organizationId);

    return await RepositorySeeder.AddRepositoryAsync(context, installationId, ExternalId, slug, Ct);
  }

  private static async Task<long> AddTokenAsync(TwoOrganizationDatabase seed, long organizationId, UploadTokenScope scope, long? repositoryId, byte value)
  {
    await using var context = seed.ContextOf(organizationId);

    var hash = new byte[UploadToken.HashLength];
    hash[0] = value;

    var token = UploadToken.Create(scope, repositoryId, "ci", "hint", hash, false, "root", Now, null);

    context.UploadTokens.Add(token);
    await context.SaveChangesAsync(Ct);

    return token.Id;
  }

  private static async Task<string> NameAsync(TwoOrganizationDatabase seed, long organizationId)
  {
    await using var context = seed.ContextOf(organizationId);

    return (await context.Repositories.AsNoTracking().SingleAsync(Ct)).Name;
  }

  private static async Task<int> VersionAsync(TwoOrganizationDatabase seed, long organizationId)
  {
    await using var context = seed.ContextOf(organizationId);

    return (await context.Repositories.AsNoTracking().SingleAsync(Ct)).Version;
  }

  private ServiceProvider BuildServices(DatabaseProvider provider, string connectionString)
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
    A.CallTo(() => gateways.GetGateway(A<Forge>._)).Returns(_forge);
    services.RemoveAll<IForgeGatewayProvider>();
    services.AddSingleton(gateways);

    services.RemoveAll<IGitHubAppGateway>();
    services.AddSingleton(_gitHub);

    // The hosts register the real one, over Data Protection.
    var protector = A.Fake<ISecretProtector>();
    A.CallTo(() => protector.Protect(A<string>._)).ReturnsLazily((string value) => $"sealed:{value}");
    services.AddSingleton(protector);

    var signatures = A.Fake<IWebhookSignatureValidator>();
    A.CallTo(() => signatures.IsValid(A<byte[]>._, A<string?>._)).Returns(true);
    services.RemoveAll<IWebhookSignatureValidator>();
    services.AddSingleton(signatures);

    return services.BuildServiceProvider();
  }

  private async Task<TwoOrganizationDatabase> SeededAsync(DatabaseProvider provider, bool admitSecond)
  {
    var seed = await TwoOrganizationDatabase.CreateAsync(database, provider, Ct);
    await AddInstallationsAsync(seed, Ct);

    await using var context = seed.ContextWithoutOrganization();
    var organizations = await context.Organizations.ToListAsync(Ct);

    foreach (var organization in organizations.Where(o => o.Id == seed.FirstId || admitSecond))
    {
      organization.Admit("root", Now);
    }

    await context.SaveChangesAsync(Ct);

    return seed;
  }

  #endregion
}
