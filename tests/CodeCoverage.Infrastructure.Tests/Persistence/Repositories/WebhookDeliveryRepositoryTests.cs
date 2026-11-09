// ------------------------------------------------------------------------------------------------
// <copyright file="WebhookDeliveryRepositoryTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Text;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

using CodeCoverage.Domain.Contracts.GitHub;
using CodeCoverage.Domain.Contracts.Jobs;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Infrastructure.Diagnostics;
using CodeCoverage.Infrastructure.Identity;
using CodeCoverage.Infrastructure.Jobs;
using CodeCoverage.Infrastructure.Persistence;
using CodeCoverage.Infrastructure.Persistence.Repositories;
using CodeCoverage.Infrastructure.Tests.Support;

using static CodeCoverage.Infrastructure.Tests.Support.RepositorySeeder;

namespace CodeCoverage.Infrastructure.Tests.Persistence.Repositories;

[Collection(DatabaseCollection.Name)]
public sealed class WebhookDeliveryRepositoryTests(DatabaseFixture database) : IDisposable
{
  #region Fields

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

    row.OrganizationId.ShouldBe(organizations.FirstId);
    row.Event.ShouldBe("push");
    row.Action.ShouldBe("created");
    row.InstallationId.ShouldBe(42);
    row.RepositoryGitHubId.ShouldBe(7);
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
