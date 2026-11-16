// ------------------------------------------------------------------------------------------------
// <copyright file="ForgeConnectionRepositoryTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

using CodeCoverage.Application.Exceptions;
using CodeCoverage.Domain.Connections;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Infrastructure.Persistence;
using CodeCoverage.Infrastructure.Persistence.Repositories;
using CodeCoverage.Infrastructure.Security;
using CodeCoverage.Infrastructure.Tests.Support;

namespace CodeCoverage.Infrastructure.Tests.Persistence.Repositories;

// A global table (ADR-017): a webhook reads it to find its organization, before the scope has entered one. Every
// reading by organization is checked against two organizations, on both engines.
[Collection(DatabaseCollection.Name)]
public sealed class ForgeConnectionRepositoryTests(DatabaseFixture database)
{
  #region Fields

  private const string PlainCredential = "plain-client-secret";

  private const string PlainWebhookSecret = "plain-webhook-secret";

  private static readonly DateTimeOffset Now = new(2026, 11, 16, 8, 0, 0, TimeSpan.Zero);

  private readonly ISecretProtector _protector = new DataProtectionSecretProtector(new EphemeralDataProtectionProvider());

  #endregion

  #region Properties

  private static CancellationToken Ct => TestContext.Current.CancellationToken;

  #endregion

  #region Methods

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
      var repository = new ForgeConnectionRepository(writer, otherProtector);
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

  private ForgeConnectionRepository Repository(CodeCoverageDbContext context) => new(context, _protector);

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
