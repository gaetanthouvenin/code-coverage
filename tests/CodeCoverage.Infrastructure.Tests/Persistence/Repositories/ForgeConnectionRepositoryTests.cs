// ------------------------------------------------------------------------------------------------
// <copyright file="ForgeConnectionRepositoryTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

using CodeCoverage.Application.Exceptions;
using CodeCoverage.Domain.Connections;
using CodeCoverage.Domain.Contracts.Jobs;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Infrastructure.Diagnostics;
using CodeCoverage.Infrastructure.Identity;
using CodeCoverage.Infrastructure.Jobs;
using CodeCoverage.Infrastructure.Persistence;
using CodeCoverage.Infrastructure.Persistence.Repositories;
using CodeCoverage.Infrastructure.Security;
using CodeCoverage.Infrastructure.Tests.Support;

namespace CodeCoverage.Infrastructure.Tests.Persistence.Repositories;

// A global table (ADR-017): a webhook reads it to find its organization, before the scope has entered one. Every
// reading by organization is checked against two organizations, on both engines.
[Collection(DatabaseCollection.Name)]
public sealed class ForgeConnectionRepositoryTests(DatabaseFixture database) : IDisposable
{
  #region Fields

  private const string PlainCredential = "plain-client-secret";

  private const string PlainWebhookSecret = "plain-webhook-secret";

  private static readonly DateTimeOffset Now = new(2026, 11, 16, 8, 0, 0, TimeSpan.Zero);

  private readonly ISecretProtector _protector = new DataProtectionSecretProtector(new EphemeralDataProtectionProvider());

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
  public async Task FindOwnerAsync_AnyOrganization_ShouldReturnItsOwner(DatabaseProvider provider)
  {
    var organizations = await TwoOrganizationDatabase.CreateAsync(database, provider, Ct);
    await AddAsync(organizations, Connection("first-conn", organizations.FirstId));
    var suspended = Connection("second-conn", organizations.SecondId);
    suspended.Suspend(Now);
    await AddAsync(organizations, suspended);

    await using (var writer = organizations.ContextWithoutOrganization())
    {
      var first = await writer.Organizations.SingleAsync(o => o.Id == organizations.FirstId, Ct);
      first.Admit("admin", Now);
      await writer.SaveChangesAsync(Ct);
    }

    await using var context = organizations.ContextWithoutOrganization();
    var repository = Repository(context);

    (await repository.FindOwnerAsync("first-conn", Ct)).ShouldBe(
      new ForgeConnectionOwner(organizations.FirstId, Forge.AzureDevOps, OrganizationState.Active, ForgeConnectionStatus.Active));
    (await repository.FindOwnerAsync("second-conn", Ct)).ShouldBe(
      new ForgeConnectionOwner(organizations.SecondId, Forge.AzureDevOps, OrganizationState.Pending, ForgeConnectionStatus.Suspended));
    (await repository.FindOwnerAsync("unknown", Ct)).ShouldBeNull();
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task ListForOrganizationAsync_TwoOrganizations_ShouldReturnOnlyItsOwn(DatabaseProvider provider)
  {
    var organizations = await TwoOrganizationDatabase.CreateAsync(database, provider, Ct);
    await AddAsync(organizations, Connection("first-b", organizations.FirstId));
    await AddAsync(organizations, Connection("first-a", organizations.FirstId));
    await AddAsync(organizations, Connection("second-a", organizations.SecondId));

    await using var context = organizations.ContextWithoutOrganization();
    var repository = Repository(context);

    (await repository.ListForOrganizationAsync(organizations.FirstId, Ct)).Select(static c => c.Id).ShouldBe(["first-a", "first-b"]);
    (await repository.ListForOrganizationAsync(organizations.SecondId, Ct)).Select(static c => c.Id).ShouldBe(["second-a"]);
    (await repository.ListForOrganizationAsync(long.MaxValue, Ct)).ShouldBeEmpty();
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task ListActiveAsync_SuspendedConnection_ShouldLeaveItOut(DatabaseProvider provider)
  {
    var organizations = await TwoOrganizationDatabase.CreateAsync(database, provider, Ct);
    await AddAsync(organizations, Connection("first-conn", organizations.FirstId));
    var failing = Connection("second-failing", organizations.SecondId);
    failing.RecordCheck(Now, "refused", TimeSpan.FromDays(14));
    await AddAsync(organizations, failing);
    var suspended = Connection("second-suspended", organizations.SecondId);
    suspended.Suspend(Now);
    await AddAsync(organizations, suspended);

    await using var context = organizations.ContextWithoutOrganization();

    (await Repository(context).ListActiveAsync(Ct)).Select(static c => c.Id).ShouldBe(["first-conn", "second-failing"]);
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task AddAsync_Secrets_ShouldStoreThemProtected(DatabaseProvider provider)
  {
    var organizations = await TwoOrganizationDatabase.CreateAsync(database, provider, Ct);
    var connection = Connection("first-conn", organizations.FirstId);
    connection.RotateWebhookSecret(_protector.Protect("next-webhook-secret"));
    await AddAsync(organizations, connection);

    await using var context = organizations.ContextWithoutOrganization();
    var stored = await context.ForgeConnections.AsNoTracking().SingleAsync(c => c.Id == "first-conn", Ct);

    stored.CredentialProtected.ShouldNotContain(PlainCredential);
    stored.WebhookSecretProtected.ShouldNotContain("next-webhook-secret");
    stored.WebhookSecretPreviousProtected.ShouldNotBeNull().ShouldNotContain(PlainWebhookSecret);
    _protector.Unprotect(stored.CredentialProtected).ShouldBe(PlainCredential);
    _protector.Unprotect(stored.WebhookSecretProtected).ShouldBe("next-webhook-secret");
    _protector.Unprotect(stored.WebhookSecretPreviousProtected).ShouldBe(PlainWebhookSecret);
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task AddAsync_PlainSecret_ShouldThrowAndStoreNothing(DatabaseProvider provider)
  {
    var organizations = await TwoOrganizationDatabase.CreateAsync(database, provider, Ct);
    var plain = Create("first-conn", organizations.FirstId, PlainCredential, _protector.Protect(PlainWebhookSecret));

    await using (var context = organizations.ContextWithoutOrganization())
    {
      await Should.ThrowAsync<InvalidOperationException>(() => Repository(context).AddAsync(plain, Ct));
    }

    await using var reader = organizations.ContextWithoutOrganization();
    (await reader.ForgeConnections.CountAsync(Ct)).ShouldBe(0);
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task AddAsync_ExistingIdentifier_ShouldThrowConflict(DatabaseProvider provider)
  {
    var organizations = await TwoOrganizationDatabase.CreateAsync(database, provider, Ct);
    await AddAsync(organizations, Connection("first-conn", organizations.FirstId));

    await using var context = organizations.ContextWithoutOrganization();

    await Should.ThrowAsync<ConflictException>(() => Repository(context).AddAsync(Connection("first-conn", organizations.SecondId), Ct));
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task AddAsync_UnknownOrganization_ShouldThrow(DatabaseProvider provider)
  {
    var organizations = await TwoOrganizationDatabase.CreateAsync(database, provider, Ct);
    await using var context = organizations.ContextWithoutOrganization();

    await Should.ThrowAsync<DbUpdateException>(() => Repository(context).AddAsync(Connection("first-conn", long.MaxValue), Ct));
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task UpdateAsync_ReadInThisScope_ShouldSaveTheChange(DatabaseProvider provider)
  {
    var organizations = await TwoOrganizationDatabase.CreateAsync(database, provider, Ct);
    await AddAsync(organizations, Connection("first-conn", organizations.FirstId));

    await using (var writer = organizations.ContextWithoutOrganization())
    {
      var repository = Repository(writer);
      var connection = (await repository.FindAsync("first-conn", Ct)).ShouldNotBeNull();
      connection.Suspend(Now.AddHours(1));
      await repository.UpdateAsync(connection, Ct);
    }

    await using var reader = organizations.ContextWithoutOrganization();
    var read = (await Repository(reader).FindAsync("first-conn", Ct)).ShouldNotBeNull();
    read.Status.ShouldBe(ForgeConnectionStatus.Suspended);
    read.SuspendedAt.ShouldBe(Now.AddHours(1));
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task UpdateAsync_NotReadInThisScope_ShouldThrow(DatabaseProvider provider)
  {
    var organizations = await TwoOrganizationDatabase.CreateAsync(database, provider, Ct);
    await using var context = organizations.ContextWithoutOrganization();

    await Should.ThrowAsync<InvalidOperationException>(() => Repository(context).UpdateAsync(Connection("first-conn", organizations.FirstId), Ct));
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task UpdateAsync_SecretsOfALostKey_ShouldSaveTheChange(DatabaseProvider provider)
  {
    var organizations = await TwoOrganizationDatabase.CreateAsync(database, provider, Ct);
    var connection = Connection("first-conn", organizations.FirstId);
    connection.RotateWebhookSecret(_protector.Protect("next-webhook-secret"));
    await AddAsync(organizations, connection);
    var otherProtector = new DataProtectionSecretProtector(new EphemeralDataProtectionProvider());

    await using (var writer = organizations.ContextWithoutOrganization())
    {
      var repository = Repository(writer, otherProtector);
      var read = (await repository.FindAsync("first-conn", Ct)).ShouldNotBeNull();
      read.RecordCheck(Now.AddHours(1), "credential cannot be decrypted", TimeSpan.FromDays(14));
      await repository.UpdateAsync(read, Ct);
      read.Suspend(Now.AddHours(2));
      await repository.UpdateAsync(read, Ct);
      read.RotateWebhookSecret(otherProtector.Protect("new-webhook-secret"));
      await repository.UpdateAsync(read, Ct);
    }

    await using var reader = organizations.ContextWithoutOrganization();
    var stored = (await Repository(reader).FindAsync("first-conn", Ct)).ShouldNotBeNull();
    stored.Status.ShouldBe(ForgeConnectionStatus.Suspended);
    stored.SuspendedAt.ShouldBe(Now.AddHours(2));
    otherProtector.Unprotect(stored.WebhookSecretProtected).ShouldBe("new-webhook-secret");
    _protector.Unprotect(stored.WebhookSecretPreviousProtected.ShouldNotBeNull()).ShouldBe("next-webhook-secret");
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task UpdateAsync_PlainSecretWritten_ShouldThrow(DatabaseProvider provider)
  {
    var organizations = await TwoOrganizationDatabase.CreateAsync(database, provider, Ct);
    await AddAsync(organizations, Connection("first-conn", organizations.FirstId));

    await using var context = organizations.ContextWithoutOrganization();
    var repository = Repository(context);
    var read = (await repository.FindAsync("first-conn", Ct)).ShouldNotBeNull();
    read.RotateWebhookSecret(PlainWebhookSecret);

    await Should.ThrowAsync<InvalidOperationException>(() => repository.UpdateAsync(read, Ct));
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task AddWithOrganizationAsync_Valid_ShouldStoreAllThree(DatabaseProvider provider)
  {
    var organizations = await TwoOrganizationDatabase.CreateAsync(database, provider, Ct);
    var organization = NewOrganization();

    await using (var context = organizations.ContextWithoutOrganization())
    {
      var added = await Repository(context).AddWithOrganizationAsync(organization, o => Connection("new-conn", o.Id), Jobs, Ct);

      organization.Id.ShouldBePositive();
      added.OrganizationId.ShouldBe(organization.Id);
    }

    await using var reader = organizations.ContextWithoutOrganization();
    var stored = await reader.Organizations.AsNoTracking().SingleAsync(o => o.Id == organization.Id, Ct);
    var connection = await reader.ForgeConnections.AsNoTracking().SingleAsync(c => c.Id == "new-conn", Ct);
    var jobs = await reader.Jobs.AsNoTracking().OrderBy(static j => j.Type).ToListAsync(Ct);

    stored.Forge.ShouldBe(Forge.AzureDevOps);
    stored.Login.ShouldBe("contoso-new");
    connection.OrganizationId.ShouldBe(organization.Id);
    _protector.Unprotect(connection.CredentialProtected).ShouldBe(PlainCredential);
    jobs.Select(static j => j.Type).ShouldBe([JobTypes.RefreshUserMemberships, JobTypes.SyncConnectionRepositories]);
    jobs[0].OrganizationId.ShouldBeNull();
    jobs[1].OrganizationId.ShouldBe(organization.Id);
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task AddWithOrganizationAsync_JobFails_ShouldRollBackBoth(DatabaseProvider provider)
  {
    var organizations = await TwoOrganizationDatabase.CreateAsync(database, provider, Ct);
    var organization = NewOrganization();

    await using (var context = organizations.ContextWithoutOrganization())
    {
      // A job of another organization is refused once the organization and its connection are written.
      await Should.ThrowAsync<InvalidOperationException>(() =>
        Repository(context).AddWithOrganizationAsync(
          organization,
          o => Connection("new-conn", o.Id),
          _ => [SyncJob(organizations.FirstId)],
          Ct));

      context.ChangeTracker.Entries().ShouldBeEmpty();
      organization.Id.ShouldBe(0);
    }

    await using var reader = organizations.ContextWithoutOrganization();
    (await reader.Organizations.AsNoTracking().CountAsync(o => o.NormalizedLogin == "contoso-new", Ct)).ShouldBe(0);
    (await reader.ForgeConnections.AsNoTracking().CountAsync(Ct)).ShouldBe(0);
    (await reader.Jobs.AsNoTracking().CountAsync(Ct)).ShouldBe(0);
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task AddWithOrganizationAsync_ExistingAccount_ShouldThrowConflictAndStoreNothing(DatabaseProvider provider)
  {
    var organizations = await TwoOrganizationDatabase.CreateAsync(database, provider, Ct);

    await using (var writer = organizations.ContextWithoutOrganization())
    {
      await Repository(writer).AddWithOrganizationAsync(NewOrganization(), o => Connection("first-conn", o.Id), Jobs, Ct);
    }

    await using (var context = organizations.ContextWithoutOrganization())
    {
      await Should.ThrowAsync<ConflictException>(() =>
        Repository(context).AddWithOrganizationAsync(NewOrganization(), o => Connection("second-conn", o.Id), Jobs, Ct));
    }

    await using var reader = organizations.ContextWithoutOrganization();
    (await reader.ForgeConnections.AsNoTracking().Select(static c => c.Id).ToListAsync(Ct)).ShouldBe(["first-conn"]);
    (await reader.Jobs.AsNoTracking().CountAsync(Ct)).ShouldBe(2);
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task AddWithOrganizationAsync_ExistingConnectionId_ShouldThrowConflictAndStoreNothing(DatabaseProvider provider)
  {
    var organizations = await TwoOrganizationDatabase.CreateAsync(database, provider, Ct);

    await using (var writer = organizations.ContextWithoutOrganization())
    {
      await Repository(writer).AddWithOrganizationAsync(NewOrganization(), o => Connection("first-conn", o.Id), Jobs, Ct);
    }

    var other = NewOrganization("account-fabrikam", "fabrikam");

    await using (var context = organizations.ContextWithoutOrganization())
    {
      await Should.ThrowAsync<ConflictException>(() =>
        Repository(context).AddWithOrganizationAsync(other, o => Connection("first-conn", o.Id), Jobs, Ct));

      context.ChangeTracker.Entries().ShouldBeEmpty();
      other.Id.ShouldBe(0);
    }

    await using var reader = organizations.ContextWithoutOrganization();
    (await reader.Organizations.AsNoTracking().CountAsync(o => o.NormalizedLogin == "fabrikam", Ct)).ShouldBe(0);
    (await reader.ForgeConnections.AsNoTracking().Select(static c => c.Id).ToListAsync(Ct)).ShouldBe(["first-conn"]);
    (await reader.Jobs.AsNoTracking().CountAsync(Ct)).ShouldBe(2);
  }

  private static Organization NewOrganization(string account = "account-contoso-new", string login = "contoso-new") =>
    Organization.CreatePending(Forge.AzureDevOps, account, login, OrganizationAccountType.Organization, Now);

  private static JobRequest SyncJob(long organizationId)
  {
    var key = RepositoryReconciliationJobs.ConnectionKey("new-conn");

    return JobRequest.ForOrganization(
      organizationId,
      JobTypes.SyncConnectionRepositories,
      RepositoryReconciliationJobs.ConnectionPayload(Forge.AzureDevOps, "new-conn"),
      key,
      $"sync:{key}",
      coalesce: true);
  }

  private static IReadOnlyList<JobRequest> Jobs(Organization organization) =>
    [SyncJob(organization.Id), RefreshUserMembershipsJobs.Create(1)];

  private ForgeConnectionRepository Repository(CodeCoverageDbContext context) => Repository(context, _protector);

  private ForgeConnectionRepository Repository(CodeCoverageDbContext context, ISecretProtector protector)
  {
    var clock = A.Fake<IClock>();
    A.CallTo(() => clock.UtcNow).Returns(Now);

    var queue = new SqlJobQueue(
      context,
      new OrganizationContext(),
      clock,
      new ConfigurationBuilder().Build(),
      JobClaimDialects.For(context),
      _wakeUpSignal,
      new CodeCoverageMeter(_meterFactory));

    return new ForgeConnectionRepository(context, protector, queue, _wakeUpSignal);
  }

  private ForgeConnection Connection(string id, long organizationId) =>
    Create(id, organizationId, _protector.Protect(PlainCredential), _protector.Protect(PlainWebhookSecret));

  private static ForgeConnection Create(string id, long organizationId, string credential, string webhookSecret) =>
    ForgeConnection.Create(
      id,
      organizationId,
      Forge.AzureDevOps,
      ForgeConnectionKind.AzureDevOpsEntraApplication,
      $"account-{id}",
      new Uri("https://dev.azure.com/contoso/"),
      Guid.NewGuid(),
      "service-account",
      credential,
      webhookSecret,
      Now.AddDays(90),
      Now);

  private async Task AddAsync(TwoOrganizationDatabase organizations, ForgeConnection connection)
  {
    await using var context = organizations.ContextWithoutOrganization();

    await Repository(context).AddAsync(connection, Ct);
  }

  #endregion
}
