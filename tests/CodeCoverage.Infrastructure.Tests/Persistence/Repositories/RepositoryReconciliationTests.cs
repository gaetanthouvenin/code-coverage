// ------------------------------------------------------------------------------------------------
// <copyright file="RepositoryReconciliationTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.EntityFrameworkCore;

using CodeCoverage.Application.Exceptions;
using CodeCoverage.Application.Exceptions.ErrorCodes;
using CodeCoverage.Domain.Contracts.Jobs;
using CodeCoverage.Domain.Repositories;
using CodeCoverage.Domain.Tokens;
using CodeCoverage.Infrastructure.Identity;
using CodeCoverage.Infrastructure.Persistence;
using CodeCoverage.Infrastructure.Tests.Support;

using static CodeCoverage.Infrastructure.Tests.Support.RepositorySeeder;

namespace CodeCoverage.Infrastructure.Tests.Persistence.Repositories;

// The saves of a repository that carry a job or the tokens of a departure, against two organizations, on both engines.
[Collection(DatabaseCollection.Name)]
public sealed class RepositoryReconciliationTests(DatabaseFixture database)
{
  #region Fields

  private const string Slug = "shared-org/shared-repo";

  private const string ExternalId = "4242";

  private static readonly DateTimeOffset Later = Now.AddDays(1);

  #endregion

  #region Properties

  private static CancellationToken Ct => TestContext.Current.CancellationToken;

  #endregion

  #region Methods

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task SaveWithJob_NewRepository_ShouldAddItAndQueueTheJobOnTheLaneOfTheInstance(DatabaseProvider provider)
  {
    var organizations = await SeededAsync(provider);

    await using (var context = organizations.ContextOf(organizations.FirstId))
    {
      await TestRepositories.Repositories(context, organizations.FirstId)
                            .SaveWithJobAsync(NewRepository(FirstInstallation, ExternalId, Slug), SyncInstallationsJobs.CreateForTransfer(ExternalId, "2002"), Ct);
    }

    await using var first = organizations.ContextOf(organizations.FirstId);
    (await TestRepositories.Repositories(first).FindByExternalIdAsync(ExternalId, Ct)).ShouldNotBeNull().IsActive.ShouldBeTrue();

    // The row belongs to the first organization only.
    await using var second = organizations.ContextOf(organizations.SecondId);
    (await TestRepositories.Repositories(second).ListAsync(Ct)).ShouldBeEmpty();

    var job = (await JobsAsync(organizations)).ShouldHaveSingleItem();
    job.Type.ShouldBe(JobTypes.SyncInstallations);
    job.OrganizationId.ShouldBeNull();
    job.LockKey.ShouldBe("i:installations");
    job.DedupKey.ShouldBe("i:installations:transfer:4242:2002");
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task SaveWithJob_TrackedRepository_ShouldSaveItsChangesAndQueueTheJob(DatabaseProvider provider)
  {
    var organizations = await SeededAsync(provider);
    await AddToBothAsync(organizations);

    await using (var context = organizations.ContextOf(organizations.FirstId))
    {
      var repositories = TestRepositories.Repositories(context, organizations.FirstId);
      var repository = (await repositories.FindByExternalIdAsync(ExternalId, Ct)).ShouldNotBeNull();

      repository.Deactivate(Later);
      await repositories.SaveWithJobAsync(repository, SyncInstallationsJobs.Create(), Ct);
    }

    await using var first = organizations.ContextOf(organizations.FirstId);
    (await TestRepositories.Repositories(first).FindByExternalIdAsync(ExternalId, Ct)).ShouldNotBeNull().IsActive.ShouldBeFalse();

    // The same repository of the other organization is not touched.
    await using var second = organizations.ContextOf(organizations.SecondId);
    (await TestRepositories.Repositories(second).FindByExternalIdAsync(ExternalId, Ct)).ShouldNotBeNull().IsActive.ShouldBeTrue();

    (await JobsAsync(organizations)).ShouldHaveSingleItem();
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task SaveWithJob_DuplicateRepository_ShouldThrowConflictAndQueueNothing(DatabaseProvider provider)
  {
    var organizations = await SeededAsync(provider);
    await AddToBothAsync(organizations);

    await using var context = organizations.ContextOf(organizations.FirstId);
    var repositories = TestRepositories.Repositories(context, organizations.FirstId);

    var exception = await Should.ThrowAsync<ConflictException>(
                      () => repositories.SaveWithJobAsync(NewRepository(FirstInstallation, ExternalId, "shared-org/other-name"), SyncInstallationsJobs.Create(), Ct)
                    );

    exception.ErrorCode.ShouldBe(GeneralErrors.ConcurrentChange);
    (await JobsAsync(organizations)).ShouldBeEmpty();

    // Nothing is left in the scope: a later save does not fail on the refused row.
    (await repositories.FindByExternalIdAsync(ExternalId, Ct)).ShouldNotBeNull().Name.ShouldBe("shared-repo");
    await context.SaveChangesAsync(Ct);
  }

  // A job that cannot be queued takes the repository back with it: one transaction.
  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task SaveWithJob_JobRefused_ShouldSaveNothing(DatabaseProvider provider)
  {
    var organizations = await SeededAsync(provider);

    await using (var context = organizations.ContextOf(organizations.FirstId))
    {
      // The queue of this scope entered the first organization: a job that names the second one is refused.
      var foreign = JobRequest.ForOrganization(organizations.SecondId, JobTypes.SyncInstallationRepositories, "{}");

      await Should.ThrowAsync<InvalidOperationException>(
        () => TestRepositories.Repositories(context, organizations.FirstId).SaveWithJobAsync(NewRepository(FirstInstallation, ExternalId, Slug), foreign, Ct)
      );
    }

    await using var check = organizations.ContextOf(organizations.FirstId);
    (await TestRepositories.Repositories(check).ListAsync(Ct)).ShouldBeEmpty();
    (await JobsAsync(organizations)).ShouldBeEmpty();
  }

  // Two handlings of the same transfer at the same time: one row, one job, and the loser is told to read again.
  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task SaveWithJob_TwoAtTheSameTime_ShouldAddTheRepositoryOnceAndQueueOnce(DatabaseProvider provider)
  {
    var organizations = await SeededAsync(provider);

    // Held after the insert of its row, before it queues its job: its transaction keeps the key of the unique index.
    var holding = new HoldingCommandInterceptor(static sql => sql.Contains("INSERT", StringComparison.OrdinalIgnoreCase)
                                                             && sql.Contains("Jobs", StringComparison.Ordinal));

    var holder = SaveInItsOwnContextAsync(organizations, provider, holding);
    await holding.Reached.Task.WaitAsync(TimeSpan.FromMinutes(1), Ct);

    var waiter = SaveInItsOwnContextAsync(organizations, provider, null);
    (await Task.WhenAny(waiter, Task.Delay(TimeSpan.FromSeconds(1), Ct))).ShouldNotBe(waiter, "the second save must wait for the key the first one holds");

    holding.Gate.SetResult();
    var saved = await Task.WhenAll(holder, waiter).WaitAsync(TimeSpan.FromMinutes(1), Ct);

    saved.ShouldBe([true, false]);

    await using var check = organizations.ContextOf(organizations.FirstId);
    (await TestRepositories.Repositories(check).ListAsync(Ct)).ShouldHaveSingleItem();
    (await JobsAsync(organizations)).ShouldHaveSingleItem();
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task SaveDeparted_ShouldRevokeTheTokensOfTheRepositoryAndMarkThoseOfTheOrganization(DatabaseProvider provider)
  {
    var organizations = await SeededAsync(provider);
    var (firstId, secondId) = await AddToBothAsync(organizations);
    long otherRepository;

    await using (var seeding = organizations.ContextOf(organizations.FirstId))
    {
      otherRepository = await AddRepositoryAsync(seeding, FirstInstallation, "77", "shared-org/other", Ct);
    }

    var revokedBefore = Now.AddDays(-3);
    var ofRepository = await AddTokenAsync(organizations, organizations.FirstId, UploadTokenScope.Repository, firstId, 1);
    var alreadyRevoked = await AddTokenAsync(organizations, organizations.FirstId, UploadTokenScope.Repository, firstId, 2, revokedBefore);
    var ofOtherRepository = await AddTokenAsync(organizations, organizations.FirstId, UploadTokenScope.Repository, otherRepository, 3);
    var ofOrganization = await AddTokenAsync(organizations, organizations.FirstId, UploadTokenScope.Organization, null, 4);
    var revokedOfOrganization = await AddTokenAsync(organizations, organizations.FirstId, UploadTokenScope.Organization, null, 5, revokedBefore);
    var foreignOfRepository = await AddTokenAsync(organizations, organizations.SecondId, UploadTokenScope.Repository, secondId, 6);
    var foreignOfOrganization = await AddTokenAsync(organizations, organizations.SecondId, UploadTokenScope.Organization, null, 7);

    RepositoryDepartureTokens result;

    await using (var context = organizations.ContextOf(organizations.FirstId))
    {
      var repositories = TestRepositories.Repositories(context, organizations.FirstId);
      var repository = (await repositories.FindAsync(firstId, Ct)).ShouldNotBeNull();

      repository.CloseAsTransferred("rotated", Later);
      result = await repositories.SaveDepartedAsync(repository, Later, Ct);
    }

    result.ShouldBe(new RepositoryDepartureTokens(1, 1));

    await using var first = organizations.ContextOf(organizations.FirstId);
    var saved = (await TestRepositories.Repositories(first).FindAsync(firstId, Ct)).ShouldNotBeNull();
    saved.IsActive.ShouldBeFalse();
    saved.TransferredAt.ShouldBe(Later);
    saved.BadgeTokenProtected.ShouldBe("rotated");

    var tokens = await first.UploadTokens.AsNoTracking().ToDictionaryAsync(static t => t.Id, Ct);
    tokens[ofRepository].RevokedAt.ShouldBe(Later);
    tokens[alreadyRevoked].RevokedAt.ShouldBe(revokedBefore);
    tokens[ofOtherRepository].RevokedAt.ShouldBeNull();
    tokens[ofOrganization].RevokedAt.ShouldBeNull();
    tokens[ofOrganization].RotationAdvisedAt.ShouldBe(Later);
    tokens[revokedOfOrganization].RotationAdvisedAt.ShouldBeNull();
    tokens[ofRepository].RotationAdvisedAt.ShouldBeNull();

    // Nothing of the other organization moved: neither its repository nor its tokens.
    await using var second = organizations.ContextOf(organizations.SecondId);
    var untouched = (await TestRepositories.Repositories(second).FindAsync(secondId, Ct)).ShouldNotBeNull();
    untouched.IsActive.ShouldBeTrue();
    untouched.TransferredAt.ShouldBeNull();
    var foreign = await second.UploadTokens.AsNoTracking().ToDictionaryAsync(static t => t.Id, Ct);
    foreign[foreignOfRepository].RevokedAt.ShouldBeNull();
    foreign[foreignOfOrganization].RotationAdvisedAt.ShouldBeNull();
  }

  // The row was only deactivated, by an installation that stopped serving the repository, and no token was touched.
  // Closing it afterwards still marks the tokens of the organization, with no token of the repository to revoke.
  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task SaveDeparted_RowDeactivatedBefore_ShouldStillMarkTheTokensOfTheOrganization(DatabaseProvider provider)
  {
    var organizations = await SeededAsync(provider);
    var (firstId, _) = await AddToBothAsync(organizations);
    var ofOrganization = await AddTokenAsync(organizations, organizations.FirstId, UploadTokenScope.Organization, null, 1);

    await using (var deactivating = organizations.ContextOf(organizations.FirstId))
    {
      var repositories = TestRepositories.Repositories(deactivating, organizations.FirstId);
      var repository = (await repositories.FindAsync(firstId, Ct)).ShouldNotBeNull();

      repository.Deactivate(Now);
      await repositories.UpdateAsync(repository, Ct);
    }

    await using var context = organizations.ContextOf(organizations.FirstId);
    var closing = TestRepositories.Repositories(context, organizations.FirstId);
    var inactive = (await closing.FindAsync(firstId, Ct)).ShouldNotBeNull();
    inactive.IsActive.ShouldBeFalse();
    inactive.TransferredAt.ShouldBeNull();
    (await context.UploadTokens.AsNoTracking().SingleAsync(t => t.Id == ofOrganization, Ct)).RotationAdvisedAt.ShouldBeNull();

    inactive.CloseAsTransferred("rotated", Later);

    (await closing.SaveDepartedAsync(inactive, Later, Ct)).ShouldBe(new RepositoryDepartureTokens(0, 1));

    await using var check = organizations.ContextOf(organizations.FirstId);
    var saved = (await TestRepositories.Repositories(check).FindAsync(firstId, Ct)).ShouldNotBeNull();
    saved.IsActive.ShouldBeFalse();
    saved.TransferredAt.ShouldBe(Later);
    saved.BadgeTokenProtected.ShouldBe("rotated");
    (await check.UploadTokens.AsNoTracking().SingleAsync(t => t.Id == ofOrganization, Ct)).RotationAdvisedAt.ShouldBe(Later);
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task SaveDeparted_Twice_ShouldKeepTheFirstRevocation(DatabaseProvider provider)
  {
    var organizations = await SeededAsync(provider);
    var (firstId, _) = await AddToBothAsync(organizations);
    var token = await AddTokenAsync(organizations, organizations.FirstId, UploadTokenScope.Repository, firstId, 1);

    await using var context = organizations.ContextOf(organizations.FirstId);
    var repositories = TestRepositories.Repositories(context, organizations.FirstId);
    var repository = (await repositories.FindAsync(firstId, Ct)).ShouldNotBeNull();
    repository.Deactivate(Later);

    (await repositories.SaveDepartedAsync(repository, Later, Ct)).Revoked.ShouldBe(1);
    (await repositories.SaveDepartedAsync(repository, Later.AddHours(1), Ct)).Revoked.ShouldBe(0);

    (await context.UploadTokens.AsNoTracking().SingleAsync(t => t.Id == token, Ct)).RevokedAt.ShouldBe(Later);
  }

  // A departure saved again, by a retry or by the other side of the transfer, marks no token created since.
  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task SaveDeparted_AgainWithNothingLeftToClose_ShouldNotMarkTheTokensCreatedSince(DatabaseProvider provider)
  {
    var organizations = await SeededAsync(provider);
    var (firstId, _) = await AddToBothAsync(organizations);
    await AddTokenAsync(organizations, organizations.FirstId, UploadTokenScope.Organization, null, 1);

    await using var context = organizations.ContextOf(organizations.FirstId);
    var repositories = TestRepositories.Repositories(context, organizations.FirstId);
    var repository = (await repositories.FindAsync(firstId, Ct)).ShouldNotBeNull();
    repository.Deactivate(Later);

    (await repositories.SaveDepartedAsync(repository, Later, Ct)).ShouldBe(new RepositoryDepartureTokens(0, 1));

    var created = await AddTokenAsync(organizations, organizations.FirstId, UploadTokenScope.Organization, null, 2);

    (await repositories.SaveDepartedAsync(repository, Later.AddHours(1), Ct)).ShouldBe(new RepositoryDepartureTokens(0, 0));
    (await context.UploadTokens.AsNoTracking().SingleAsync(t => t.Id == created, Ct)).RotationAdvisedAt.ShouldBeNull();
  }

  // A name held by another row of the organization: refused as a conflict, and the scope stays usable.
  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task Update_NameHeldByAnotherRow_ShouldThrowConflictAndLeaveTheScopeUsable(DatabaseProvider provider)
  {
    var organizations = await SeededAsync(provider);
    await using var context = organizations.ContextOf(organizations.FirstId);
    await AddRepositoryAsync(context, FirstInstallation, "1", "shared-org/taken", Ct);
    var id = await AddRepositoryAsync(context, FirstInstallation, "2", "shared-org/free", Ct);
    var repositories = TestRepositories.Repositories(context, organizations.FirstId);
    var repository = (await repositories.FindAsync(id, Ct)).ShouldNotBeNull();

    repository.Rename(RepositorySlug.Parse("shared-org/TAKEN"), Later);

    (await Should.ThrowAsync<ConflictException>(() => repositories.UpdateAsync(repository, Ct))).ErrorCode.ShouldBe(GeneralErrors.ConcurrentChange);
    await Should.ThrowAsync<ConflictException>(() => repositories.SaveWithJobAsync(Renamed(repository), SyncInstallationsJobs.Create(), Ct));

    // The refused name is gone from the scope: the instance reads as its row again, and a later save passes.
    repository.Name.ShouldBe("free");
    repository.Deactivate(Later);
    await repositories.UpdateAsync(repository, Ct);
    (await JobsAsync(organizations)).ShouldBeEmpty();

    await using var check = organizations.ContextOf(organizations.FirstId);
    var saved = (await TestRepositories.Repositories(check).FindAsync(id, Ct)).ShouldNotBeNull();
    saved.Name.ShouldBe("free");
    saved.IsActive.ShouldBeFalse();

    static TrackedRepository Renamed(TrackedRepository tracked)
    {
      tracked.Rename(RepositorySlug.Parse("shared-org/taken"), Later);

      return tracked;
    }
  }

  // The same name in another organization is no conflict.
  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task Update_NameHeldInAnotherOrganization_ShouldPass(DatabaseProvider provider)
  {
    var organizations = await SeededAsync(provider);

    await using (var second = organizations.ContextOf(organizations.SecondId))
    {
      await AddRepositoryAsync(second, SecondInstallation, "1", "shared-org/taken", Ct);
    }

    await using var context = organizations.ContextOf(organizations.FirstId);
    var id = await AddRepositoryAsync(context, FirstInstallation, "2", "shared-org/free", Ct);
    var repositories = TestRepositories.Repositories(context, organizations.FirstId);
    var repository = (await repositories.FindAsync(id, Ct)).ShouldNotBeNull();

    repository.Rename(RepositorySlug.Parse("shared-org/taken"), Later);
    await repositories.UpdateAsync(repository, Ct);

    (await repositories.FindBySlugAsync(RepositorySlug.Parse("shared-org/taken"), Ct)).ShouldNotBeNull().Id.ShouldBe(id);
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task SaveDeparted_RepositoryNotReadInTheScope_ShouldThrow(DatabaseProvider provider)
  {
    var organizations = await SeededAsync(provider);
    await using var context = organizations.ContextOf(organizations.FirstId);

    await Should.ThrowAsync<InvalidOperationException>(
      () => TestRepositories.Repositories(context, organizations.FirstId).SaveDepartedAsync(NewRepository(FirstInstallation, ExternalId, Slug), Later, Ct)
    );
  }

  // A row read by the scope of another organization cannot be closed from here: the write guard refuses it whole.
  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task SaveDeparted_RepositoryOfAnotherOrganization_ShouldWriteNothing(DatabaseProvider provider)
  {
    var organizations = await SeededAsync(provider);
    var (_, secondId) = await AddToBothAsync(organizations);
    var foreignToken = await AddTokenAsync(organizations, organizations.SecondId, UploadTokenScope.Repository, secondId, 1);

    await using var first = organizations.ContextOf(organizations.FirstId);

    (await TestRepositories.Repositories(first, organizations.FirstId).FindAsync(secondId, Ct)).ShouldBeNull();

    await using var second = organizations.ContextOf(organizations.SecondId);
    (await second.UploadTokens.AsNoTracking().SingleAsync(t => t.Id == foreignToken, Ct)).RevokedAt.ShouldBeNull();
  }

  private static async Task<IReadOnlyList<Infrastructure.Jobs.JobRecord>> JobsAsync(TwoOrganizationDatabase organizations)
  {
    await using var context = organizations.ContextWithoutOrganization();

    return await context.Jobs.AsNoTracking().ToListAsync(Ct);
  }

  private static async Task<long> AddTokenAsync(
    TwoOrganizationDatabase organizations,
    long organizationId,
    UploadTokenScope scope,
    long? repositoryId,
    byte seed,
    DateTimeOffset? revokedAt = null)
  {
    await using var context = organizations.ContextOf(organizationId);

    var hash = new byte[UploadToken.HashLength];
    hash[0] = seed;

    var token = UploadToken.Create(scope, repositoryId, "ci", "hint", hash, false, "octocat", Now, null);

    if (revokedAt is { } at)
    {
      token.Revoke(at);
    }

    context.UploadTokens.Add(token);
    await context.SaveChangesAsync(Ct);

    return token.Id;
  }

  private static async Task<(long FirstId, long SecondId)> AddToBothAsync(TwoOrganizationDatabase organizations)
  {
    await using var first = organizations.ContextOf(organizations.FirstId);
    await using var second = organizations.ContextOf(organizations.SecondId);

    var firstId = await AddRepositoryAsync(first, FirstInstallation, ExternalId, Slug, Ct);
    var secondId = await AddRepositoryAsync(second, SecondInstallation, ExternalId, Slug, Ct);

    return (firstId, secondId);
  }

  private static async Task<bool> SaveInItsOwnContextAsync(TwoOrganizationDatabase organizations, DatabaseProvider provider, HoldingCommandInterceptor? interceptor)
  {
    var options = new DbContextOptionsBuilder<CodeCoverageDbContext>();
    options.UseCodeCoverageDatabase(provider, organizations.ConnectionString);

    if (interceptor is not null)
    {
      options.AddInterceptors(interceptor);
    }

    var organization = new OrganizationContext();
    organization.Enter(organizations.FirstId);

    await using var context = new CodeCoverageDbContext(options.Options, organization);

    try
    {
      await TestRepositories.Repositories(context, organizations.FirstId)
                            .SaveWithJobAsync(NewRepository(FirstInstallation, ExternalId, Slug), SyncInstallationsJobs.CreateForTransfer(ExternalId, "2002"), Ct);

      return true;
    }
    catch (ConflictException exception) when (exception.ErrorCode == GeneralErrors.ConcurrentChange)
    {
      return false;
    }
  }

  private async Task<TwoOrganizationDatabase> SeededAsync(DatabaseProvider provider)
  {
    var organizations = await TwoOrganizationDatabase.CreateAsync(database, provider, Ct);
    await AddInstallationsAsync(organizations, Ct);

    return organizations;
  }

  #endregion
}