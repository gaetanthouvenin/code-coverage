// ------------------------------------------------------------------------------------------------
// <copyright file="WebhookDeliveryRepositoryTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.IO.Compression;
using System.Text;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;

using CodeCoverage.Domain.Contracts.Forges;
using CodeCoverage.Domain.Contracts.GitHub;
using CodeCoverage.Domain.Contracts.Jobs;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Infrastructure.Diagnostics;
using CodeCoverage.Infrastructure.Identity;
using CodeCoverage.Infrastructure.Jobs;
using CodeCoverage.Infrastructure.Persistence;
using CodeCoverage.Infrastructure.Persistence.Repositories;
using CodeCoverage.Infrastructure.Tests.Support;
using CodeCoverage.Infrastructure.Webhooks;

using static CodeCoverage.Infrastructure.Tests.Support.RepositorySeeder;

namespace CodeCoverage.Infrastructure.Tests.Persistence.Repositories;

[Collection(DatabaseCollection.Name)]
public sealed class WebhookDeliveryRepositoryTests(DatabaseFixture database) : IDisposable
{
  #region Fields

  private const string ForgeMigration = "AddWebhookDeliveryForge";

  private static readonly byte[] ForgePayload = Encoding.UTF8.GetBytes("""{"eventType":"git.push","resource":{"refUpdates":[]}}""");

  private readonly TestMeterFactory _meterFactory = new();

  private readonly JobWakeUpSignal _wakeUpSignal = new(TimeProvider.System);

  #endregion

  #region Properties

  private static CancellationToken Ct => TestContext.Current.CancellationToken;

  #endregion

  #region Methods

  public void Dispose() => _meterFactory.Dispose();

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task TryStore_ForOrganization_ShouldStoreTheRowAndQueueInItsLane(DatabaseProvider provider)
  {
    var organizations = await TwoOrganizationDatabase.CreateAsync(database, provider, Ct);

    (await StoreAsync(organizations, Delivery("d-1"), ForOrganization(organizations.FirstId))).ShouldBeTrue();

    await using var reader = organizations.ContextWithoutOrganization();
    var row = await reader.WebhookDeliveries.AsNoTracking().SingleAsync(Ct);
    var job = await reader.Jobs.AsNoTracking().SingleAsync(Ct);

    row.Forge.ShouldBe(Forge.GitHub);
    row.OrganizationId.ShouldBe(organizations.FirstId);
    row.Event.ShouldBe("push");
    row.Action.ShouldBe("created");
    row.ConnectionId.ShouldBe("42");
    row.RepositoryExternalId.ShouldBe("7");
    row.ReceivedAt.ShouldBe(Now);
    row.ProcessedAt.ShouldBeNull();
    row.Payload.ShouldNotBe(Delivery("d-1").Payload, "the body is stored compressed");
    job.OrganizationId.ShouldBe(organizations.FirstId);
    job.Type.ShouldBe(JobTypes.HandleWebhook);
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task TryStore_InstanceLane_ShouldLeaveTheOrganizationEmpty(DatabaseProvider provider)
  {
    var organizations = await TwoOrganizationDatabase.CreateAsync(database, provider, Ct);

    await StoreAsync(organizations, Delivery("d-1"), JobRequest.ForInstance(JobTypes.HandleWebhook, "{}"));

    await using var reader = organizations.ContextWithoutOrganization();

    (await reader.WebhookDeliveries.AsNoTracking().SingleAsync(Ct)).OrganizationId.ShouldBeNull();
    (await reader.Jobs.AsNoTracking().SingleAsync(Ct)).OrganizationId.ShouldBeNull();
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task TryStore_KnownDelivery_ShouldReturnFalseAndQueueNothing(DatabaseProvider provider)
  {
    var organizations = await TwoOrganizationDatabase.CreateAsync(database, provider, Ct);
    await StoreAsync(organizations, Delivery("d-1"), ForOrganization(organizations.FirstId));

    (await StoreAsync(organizations, Delivery("d-1"), ForOrganization(organizations.FirstId))).ShouldBeFalse();

    await using var reader = organizations.ContextWithoutOrganization();

    (await reader.WebhookDeliveries.AsNoTracking().CountAsync(Ct)).ShouldBe(1);
    (await reader.Jobs.AsNoTracking().CountAsync(Ct)).ShouldBe(1);
  }

  // Two forges may send the same identifier: the primary key holds the forge, so neither delivery refuses the other,
  // and the GitHub methods only touch the GitHub one.
  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task AddAsync_SameDeliveryIdOnTwoForges_ShouldKeepBoth(DatabaseProvider provider)
  {
    var organizations = await TwoOrganizationDatabase.CreateAsync(database, provider, Ct);

    await using (var context = organizations.ContextWithoutOrganization())
    {
      context.WebhookDeliveries.Add(
        new WebhookDeliveryRecord
        {
          Forge = Forge.Bitbucket,
          DeliveryId = "d-1",
          OrganizationId = organizations.FirstId,
          Event = "repo:push",
          ConnectionId = "contoso-bitbucket",
          RepositoryExternalId = "{2f1c}",
          Payload = [1],
          ReceivedAt = Now,
        }
      );
      await context.SaveChangesAsync(Ct);
    }

    (await StoreAsync(organizations, Delivery("d-1"), ForOrganization(organizations.FirstId))).ShouldBeTrue();
    await MarkAsync(organizations, "d-1", organizations.FirstId, Now.AddMinutes(1));

    await using var reader = organizations.ContextWithoutOrganization();
    var rows = await reader.WebhookDeliveries.AsNoTracking().OrderBy(static d => d.Event).ToListAsync(Ct);

    rows.Select(static d => d.Forge).ShouldBe([Forge.GitHub, Forge.Bitbucket]);
    rows[0].ProcessedAt.ShouldBe(Now.AddMinutes(1));
    rows[1].ProcessedAt.ShouldBeNull();
    rows[1].ConnectionId.ShouldBe("contoso-bitbucket");
    (await FindAsync(organizations, "d-1", organizations.FirstId)).ShouldNotBeNull().Event.ShouldBe("push");

    var listed = await Repository(reader, new OrganizationContext()).ListRecentAsync(10, Ct);

    listed.Select(static d => d.Forge).ShouldBe([Forge.Bitbucket, Forge.GitHub], ignoreOrder: true);
    listed.Single(static d => d.Forge is Forge.GitHub).ConnectionId.ShouldBe("42");
    listed.Single(static d => d.Forge is Forge.GitHub).RepositoryExternalId.ShouldBe("7");
  }

  // The migration runs on databases whose deliveries all came from GitHub: each one is read back on the forge GitHub
  // with its installation and its repository, so it can still be handled again.
  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task Migration_ExistingGitHubDelivery_ShouldKeepItsIdentifiers(DatabaseProvider provider)
  {
    var organizations = await TwoOrganizationDatabase.CreateAsync(database, provider, Ct);
    await StoreAsync(organizations, Delivery("d-1"), ForOrganization(organizations.FirstId));

    await MigrateAsync(organizations, MigrationBefore(organizations, ForgeMigration));
    await MigrateAsync(organizations, null);

    var found = await FindAsync(organizations, "d-1", organizations.FirstId);

    found.ShouldNotBeNull().Payload.ShouldBe(Delivery("d-1").Payload);
    found.InstallationId.ShouldBe(42);
    found.RepositoryId.ShouldBe(7);

    await using var check = organizations.ContextWithoutOrganization();
    var row = await check.WebhookDeliveries.AsNoTracking().SingleAsync(Ct);

    row.Forge.ShouldBe(Forge.GitHub);
    row.ConnectionId.ShouldBe("42");
    row.RepositoryExternalId.ShouldBe("7");
    row.OrganizationId.ShouldBe(organizations.FirstId);
    (await check.Database.GetPendingMigrationsAsync(Ct)).ShouldBeEmpty();
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task TryStore_Concurrent_ShouldStoreOnceAndQueueOnce(DatabaseProvider provider)
  {
    var organizations = await TwoOrganizationDatabase.CreateAsync(database, provider, Ct);

    var results = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => StoreAsync(organizations, Delivery("d-1"), ForOrganization(organizations.FirstId))));

    results.Count(static stored => stored).ShouldBe(1);

    await using var reader = organizations.ContextWithoutOrganization();

    (await reader.Jobs.AsNoTracking().CountAsync(Ct)).ShouldBe(1);
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task TryStore_JobWithoutLane_ShouldThrow(DatabaseProvider provider)
  {
    var organizations = await TwoOrganizationDatabase.CreateAsync(database, provider, Ct);
    await using var context = organizations.ContextWithoutOrganization();

    await Should.ThrowAsync<ArgumentException>(
      () => Repository(context, new OrganizationContext()).TryStoreAsync(Delivery("d-1"), JobRequest.Create(JobTypes.HandleWebhook, "{}"), Now, Ct)
    );
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task FindForProcessing_SameOrganization_ShouldGiveTheBodyBack(DatabaseProvider provider)
  {
    var organizations = await TwoOrganizationDatabase.CreateAsync(database, provider, Ct);
    await StoreAsync(organizations, Delivery("d-1"), ForOrganization(organizations.FirstId));

    var found = await FindAsync(organizations, "d-1", organizations.FirstId);

    found.ShouldNotBeNull().Payload.ShouldBe(Delivery("d-1").Payload);
    found.Action.ShouldBe("created");
    found.InstallationId.ShouldBe(42);
    found.RepositoryId.ShouldBe(7);
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task FindForProcessing_OtherOrganization_ShouldReturnNull(DatabaseProvider provider)
  {
    var organizations = await TwoOrganizationDatabase.CreateAsync(database, provider, Ct);
    await StoreAsync(organizations, Delivery("d-1"), ForOrganization(organizations.FirstId));

    (await FindAsync(organizations, "d-1", organizations.SecondId)).ShouldBeNull();
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task FindForProcessing_RowOfAnOrganizationWithoutScope_ShouldReturnNull(DatabaseProvider provider)
  {
    var organizations = await TwoOrganizationDatabase.CreateAsync(database, provider, Ct);
    await StoreAsync(organizations, Delivery("d-1"), ForOrganization(organizations.FirstId));

    (await FindAsync(organizations, "d-1", organizationId: null)).ShouldBeNull();
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task FindForProcessing_RowOfTheInstance_ShouldBeReadByAnyScope(DatabaseProvider provider)
  {
    var organizations = await TwoOrganizationDatabase.CreateAsync(database, provider, Ct);
    await StoreAsync(organizations, Delivery("d-1"), JobRequest.ForInstance(JobTypes.HandleWebhook, "{}"));

    (await FindAsync(organizations, "d-1", organizationId: null)).ShouldNotBeNull();
    (await FindAsync(organizations, "d-1", organizations.SecondId)).ShouldNotBeNull();
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task FindForProcessing_Unknown_ShouldReturnNull(DatabaseProvider provider)
  {
    var organizations = await TwoOrganizationDatabase.CreateAsync(database, provider, Ct);

    (await FindAsync(organizations, "unknown", organizations.FirstId)).ShouldBeNull();
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task ListRecent_ShouldGiveTheLatestFirstAndHonorTheCount(DatabaseProvider provider)
  {
    var organizations = await TwoOrganizationDatabase.CreateAsync(database, provider, Ct);
    await StoreAsync(organizations, Delivery("old"), ForOrganization(organizations.FirstId), Now);
    await StoreAsync(organizations, Delivery("middle"), ForOrganization(organizations.SecondId), Now.AddMinutes(1));
    await StoreAsync(organizations, Delivery("new"), JobRequest.ForInstance(JobTypes.HandleWebhook, "{}"), Now.AddMinutes(2));

    await using var context = organizations.ContextWithoutOrganization();
    var listed = await Repository(context, new OrganizationContext()).ListRecentAsync(2, Ct);

    listed.Select(static d => d.DeliveryId).ShouldBe(["new", "middle"]);
    listed[1].OrganizationId.ShouldBe(organizations.SecondId);
    listed[0].OrganizationId.ShouldBeNull();
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task MarkProcessed_SameOrganization_ShouldKeepTheFirstDate(DatabaseProvider provider)
  {
    var organizations = await TwoOrganizationDatabase.CreateAsync(database, provider, Ct);
    await StoreAsync(organizations, Delivery("d-1"), ForOrganization(organizations.FirstId));

    await MarkAsync(organizations, "d-1", organizations.FirstId, Now.AddMinutes(1));
    await MarkAsync(organizations, "d-1", organizations.FirstId, Now.AddMinutes(5));

    (await ProcessedAtAsync(organizations, "d-1")).ShouldBe(Now.AddMinutes(1));
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task MarkProcessed_OtherOrganizationOrNoScope_ShouldLeaveTheRow(DatabaseProvider provider)
  {
    var organizations = await TwoOrganizationDatabase.CreateAsync(database, provider, Ct);
    await StoreAsync(organizations, Delivery("d-1"), ForOrganization(organizations.FirstId));

    await MarkAsync(organizations, "d-1", organizations.SecondId, Now.AddMinutes(1));
    await MarkAsync(organizations, "d-1", null, Now.AddMinutes(1));

    (await ProcessedAtAsync(organizations, "d-1")).ShouldBeNull();
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task MarkProcessed_RowOfTheInstance_ShouldBeMarkedFromTheLaneOfTheInstance(DatabaseProvider provider)
  {
    var organizations = await TwoOrganizationDatabase.CreateAsync(database, provider, Ct);
    await StoreAsync(organizations, Delivery("d-1"), JobRequest.ForInstance(JobTypes.HandleWebhook, "{}"));
    await StoreAsync(organizations, Delivery("d-2"), JobRequest.ForInstance(JobTypes.HandleWebhook, "{}"));

    await MarkAsync(organizations, "d-1", null, Now.AddMinutes(1));

    (await ProcessedAtAsync(organizations, "d-1")).ShouldBe(Now.AddMinutes(1));
    (await ProcessedAtAsync(organizations, "d-2")).ShouldBeNull();
  }

  // M7.20. A delivery of a forge with connections is read back on its forge, with its connection and its body.
  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task FindForgeDeliveryForProcessing_SameOrganization_ShouldGiveTheDeliveryBack(DatabaseProvider provider)
  {
    var organizations = await TwoOrganizationDatabase.CreateAsync(database, provider, Ct);
    await AddForgeDeliveryAsync(organizations, "d-1", organizations.FirstId, "contoso-devops");

    var found = await FindForgeDeliveryAsync(organizations, Forge.AzureDevOps, "d-1", organizations.FirstId);

    found.ShouldNotBeNull().Forge.ShouldBe(Forge.AzureDevOps);
    found.DeliveryId.ShouldBe("d-1");
    found.Event.ShouldBe("git.push");
    found.Action.ShouldBe("updated");
    found.ConnectionId.ShouldBe("contoso-devops");
    found.RepositoryExternalId.ShouldBe("repo-guid");
    found.Payload.ShouldBe(ForgePayload);
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task FindForgeDeliveryForProcessing_OtherOrganizationOrForge_ShouldReturnNull(DatabaseProvider provider)
  {
    var organizations = await TwoOrganizationDatabase.CreateAsync(database, provider, Ct);
    await AddForgeDeliveryAsync(organizations, "d-1", organizations.FirstId, "contoso-devops");

    (await FindForgeDeliveryAsync(organizations, Forge.AzureDevOps, "d-1", organizations.SecondId)).ShouldBeNull();
    (await FindForgeDeliveryAsync(organizations, Forge.AzureDevOps, "d-1", organizationId: null)).ShouldBeNull();
    (await FindForgeDeliveryAsync(organizations, Forge.Bitbucket, "d-1", organizations.FirstId)).ShouldBeNull();
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task FindForgeDeliveryForProcessing_WithoutConnection_ShouldReturnNull(DatabaseProvider provider)
  {
    var organizations = await TwoOrganizationDatabase.CreateAsync(database, provider, Ct);
    await AddForgeDeliveryAsync(organizations, "d-1", organizations.FirstId, connectionId: null);

    (await FindForgeDeliveryAsync(organizations, Forge.AzureDevOps, "d-1", organizations.FirstId)).ShouldBeNull();
  }

  // The GitHub delivery of the same identifier is left alone, and a delivery handled again keeps its first date.
  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task MarkForgeDeliveryProcessed_ShouldMarkOnlyTheDeliveryOfThatForge(DatabaseProvider provider)
  {
    var organizations = await TwoOrganizationDatabase.CreateAsync(database, provider, Ct);
    await StoreAsync(organizations, Delivery("d-1"), ForOrganization(organizations.FirstId));
    await AddForgeDeliveryAsync(organizations, "d-1", organizations.FirstId, "contoso-devops");

    await MarkForgeDeliveryAsync(organizations, "d-1", organizations.SecondId, Now.AddMinutes(1));
    await MarkForgeDeliveryAsync(organizations, "d-1", organizations.FirstId, Now.AddMinutes(2));
    await MarkForgeDeliveryAsync(organizations, "d-1", organizations.FirstId, Now.AddMinutes(5));

    await using var reader = organizations.ContextWithoutOrganization();
    var rows = await reader.WebhookDeliveries.AsNoTracking().ToListAsync(Ct);

    rows.Single(static d => d.Forge is Forge.AzureDevOps).ProcessedAt.ShouldBe(Now.AddMinutes(2));
    rows.Single(static d => d.Forge is Forge.GitHub).ProcessedAt.ShouldBeNull();
  }

  // M7.21. A delivery of a forge with connections is stored with its forge and its connection, in the organization of
  // its job, and read back by the job of that organization.
  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task TryStoreForgeDelivery_ForOrganization_ShouldStoreTheRowAndQueueInItsLane(DatabaseProvider provider)
  {
    var organizations = await TwoOrganizationDatabase.CreateAsync(database, provider, Ct);

    (await StoreForgeDeliveryAsync(organizations, ConnectionDelivery("d-1"), ForgeJob(organizations.FirstId, "d-1"))).ShouldBeTrue();

    await using (var reader = organizations.ContextWithoutOrganization())
    {
      var row = await reader.WebhookDeliveries.AsNoTracking().SingleAsync(Ct);
      var job = await reader.Jobs.AsNoTracking().SingleAsync(Ct);

      row.Forge.ShouldBe(Forge.AzureDevOps);
      row.DeliveryId.ShouldBe("d-1");
      row.OrganizationId.ShouldBe(organizations.FirstId);
      row.Event.ShouldBe("git.push");
      row.Action.ShouldBeNull();
      row.ConnectionId.ShouldBe("contoso-devops");
      row.RepositoryExternalId.ShouldBeNull();
      row.ReceivedAt.ShouldBe(Now);
      row.ProcessedAt.ShouldBeNull();
      job.OrganizationId.ShouldBe(organizations.FirstId);
      job.Type.ShouldBe(JobTypes.HandleForgeDelivery);
    }

    var found = await FindForgeDeliveryAsync(organizations, Forge.AzureDevOps, "d-1", organizations.FirstId);

    found.ShouldNotBeNull().ConnectionId.ShouldBe("contoso-devops");
    found.Payload.ShouldBe(ForgePayload);
    (await FindForgeDeliveryAsync(organizations, Forge.AzureDevOps, "d-1", organizations.SecondId)).ShouldBeNull();
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task TryStoreForgeDelivery_KnownDelivery_ShouldReturnFalseAndQueueNothing(DatabaseProvider provider)
  {
    var organizations = await TwoOrganizationDatabase.CreateAsync(database, provider, Ct);
    await StoreForgeDeliveryAsync(organizations, ConnectionDelivery("d-1"), ForgeJob(organizations.FirstId, "d-1"));

    (await StoreForgeDeliveryAsync(organizations, ConnectionDelivery("d-1"), ForgeJob(organizations.FirstId, "d-1"))).ShouldBeFalse();

    await using var reader = organizations.ContextWithoutOrganization();

    (await reader.WebhookDeliveries.AsNoTracking().CountAsync(Ct)).ShouldBe(1);
    (await reader.Jobs.AsNoTracking().CountAsync(Ct)).ShouldBe(1);
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task TryStoreForgeDelivery_GitHubOrJobWithoutOrganization_ShouldThrow(DatabaseProvider provider)
  {
    var organizations = await TwoOrganizationDatabase.CreateAsync(database, provider, Ct);
    var gitHub = new ForgeDelivery(Forge.GitHub, "d-1", "push", null, "42", null, ForgePayload);

    await Should.ThrowAsync<ArgumentException>(() => StoreForgeDeliveryAsync(organizations, gitHub, ForgeJob(organizations.FirstId, "d-1")));
    await Should.ThrowAsync<ArgumentException>(
      () => StoreForgeDeliveryAsync(organizations, ConnectionDelivery("d-1"), JobRequest.ForInstance(JobTypes.HandleForgeDelivery, "{}"))
    );

    await using var reader = organizations.ContextWithoutOrganization();

    (await reader.WebhookDeliveries.AsNoTracking().CountAsync(Ct)).ShouldBe(0);
  }

  private static ForgeDelivery ConnectionDelivery(string id) => new(Forge.AzureDevOps, id, "git.push", null, "contoso-devops", null, ForgePayload);

  private static JobRequest ForgeJob(long organizationId, string id) =>
    JobRequest.ForOrganization(organizationId, JobTypes.HandleForgeDelivery, ForgeDeliveryJobs.Payload(Forge.AzureDevOps, id));

  private async Task<bool> StoreForgeDeliveryAsync(TwoOrganizationDatabase organizations, ForgeDelivery delivery, JobRequest job)
  {
    await using var context = organizations.ContextWithoutOrganization();

    return await Repository(context, new OrganizationContext()).TryStoreForgeDeliveryAsync(delivery, job, Now, Ct);
  }

  private static async Task AddForgeDeliveryAsync(TwoOrganizationDatabase organizations, string id, long organizationId, string? connectionId)
  {
    using var output = new MemoryStream();

    await using (var gzip = new GZipStream(output, CompressionLevel.Fastest, leaveOpen: true))
    {
      await gzip.WriteAsync(ForgePayload, Ct);
    }

    await using var context = organizations.ContextWithoutOrganization();

    context.WebhookDeliveries.Add(
      new WebhookDeliveryRecord
      {
        Forge = Forge.AzureDevOps,
        DeliveryId = id,
        OrganizationId = organizationId,
        Event = "git.push",
        Action = "updated",
        ConnectionId = connectionId,
        RepositoryExternalId = "repo-guid",
        Payload = output.ToArray(),
        ReceivedAt = Now,
      }
    );
    await context.SaveChangesAsync(Ct);
  }

  private async Task<ForgeDelivery?> FindForgeDeliveryAsync(TwoOrganizationDatabase organizations, Forge forge, string id, long? organizationId)
  {
    var organization = new OrganizationContext();

    if (organizationId is { } value)
    {
      organization.Enter(value);
    }

    await using var context = organizationId is { } entered ? organizations.ContextOf(entered) : organizations.ContextWithoutOrganization();

    return await Repository(context, organization).FindForgeDeliveryForProcessingAsync(forge, id, Ct);
  }

  private async Task MarkForgeDeliveryAsync(TwoOrganizationDatabase organizations, string id, long organizationId, DateTimeOffset at)
  {
    var organization = new OrganizationContext();
    organization.Enter(organizationId);

    await using var context = organizations.ContextOf(organizationId);

    await Repository(context, organization).MarkForgeDeliveryProcessedAsync(Forge.AzureDevOps, id, at, Ct);
  }

  // The migration applied just before the named one.
  private static string MigrationBefore(TwoOrganizationDatabase organizations, string migration)
  {
    using var context = organizations.ContextWithoutOrganization();
    var migrations = context.Database.GetMigrations().ToList();
    var index = migrations.FindIndex(id => id.EndsWith("_" + migration, StringComparison.Ordinal));

    index.ShouldBeGreaterThan(0);

    return migrations[index - 1];
  }

  private static async Task MigrateAsync(TwoOrganizationDatabase organizations, string? target)
  {
    await using var context = organizations.ContextWithoutOrganization();

    await context.GetService<IMigrator>().MigrateAsync(target, Ct);
  }

  private static async Task<DateTimeOffset?> ProcessedAtAsync(TwoOrganizationDatabase organizations, string id)
  {
    await using var context = organizations.ContextWithoutOrganization();

    return await context.WebhookDeliveries.AsNoTracking().Where(d => d.DeliveryId == id).Select(static d => d.ProcessedAt).SingleAsync(Ct);
  }

  private async Task MarkAsync(TwoOrganizationDatabase organizations, string id, long? organizationId, DateTimeOffset at)
  {
    var organization = new OrganizationContext();

    if (organizationId is { } value)
    {
      organization.Enter(value);
    }

    await using var context = organizationId is { } entered ? organizations.ContextOf(entered) : organizations.ContextWithoutOrganization();

    await Repository(context, organization).MarkProcessedAsync(id, at, Ct);
  }

  private static GitHubWebhookEvent Delivery(string id) =>
    GitHubWebhookEvent.TryParse(id, "push", Encoding.UTF8.GetBytes("""{"action":"created","installation":{"id":42},"repository":{"id":7},"padding":"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"}"""))!;

  private static JobRequest ForOrganization(long organizationId) => JobRequest.ForOrganization(organizationId, JobTypes.HandleWebhook, "{}");

  private async Task<bool> StoreAsync(TwoOrganizationDatabase organizations, GitHubWebhookEvent delivery, JobRequest job, DateTimeOffset? at = null)
  {
    await using var context = organizations.ContextWithoutOrganization();

    return await Repository(context, new OrganizationContext()).TryStoreAsync(delivery, job, at ?? Now, Ct);
  }

  private async Task<GitHubWebhookEvent?> FindAsync(TwoOrganizationDatabase organizations, string id, long? organizationId)
  {
    var organization = new OrganizationContext();

    if (organizationId is { } value)
    {
      organization.Enter(value);
    }

    await using var context = organizationId is { } entered ? organizations.ContextOf(entered) : organizations.ContextWithoutOrganization();

    return await Repository(context, organization).FindForProcessingAsync(id, Ct);
  }

  private WebhookDeliveryRepository Repository(CodeCoverageDbContext context, OrganizationContext organization)
  {
    var clock = A.Fake<IClock>();
    A.CallTo(() => clock.UtcNow).Returns(Now);

    var queue = new SqlJobQueue(
      context,
      organization,
      clock,
      new ConfigurationBuilder().Build(),
      JobClaimDialects.For(context),
      _wakeUpSignal,
      new CodeCoverageMeter(_meterFactory)
    );

    return new WebhookDeliveryRepository(context, queue, _wakeUpSignal, organization);
  }

  #endregion
}
