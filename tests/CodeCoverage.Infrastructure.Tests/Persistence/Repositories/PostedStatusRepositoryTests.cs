// ------------------------------------------------------------------------------------------------
// <copyright file="PostedStatusRepositoryTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.EntityFrameworkCore;

using CodeCoverage.Application.Exceptions;
using CodeCoverage.Application.Exceptions.ErrorCodes;
using CodeCoverage.Domain.Notifications;
using CodeCoverage.Infrastructure.Identity;
using CodeCoverage.Infrastructure.Persistence;
using CodeCoverage.Infrastructure.Persistence.Repositories;
using CodeCoverage.Infrastructure.Tests.Support;

using static CodeCoverage.Infrastructure.Tests.Support.RepositorySeeder;

namespace CodeCoverage.Infrastructure.Tests.Persistence.Repositories;

// Every method against two organizations, on both engines: the rows of the other one are neither read nor written.
[Collection(DatabaseCollection.Name)]
public sealed class PostedStatusRepositoryTests(DatabaseFixture database)
{
  #region Fields

  private const string Url = "https://coverage.example.com/gh/first-org/app/commit/a94a8fe";

  #endregion

  #region Properties

  private static CancellationToken Ct => TestContext.Current.CancellationToken;

  #endregion

  #region Methods

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task Upsert_SameContext_ShouldReplace(DatabaseProvider provider)
  {
    var seed = await SeededAsync(provider);
    var first = seed.Organizations.FirstId;

    await UpsertAsync(seed, first, Posted(seed.FirstCommit, "coverage/project", StatusState.Failure, "79.00% (-1.00%) compared to bbbbbbb", Now));
    await UpsertAsync(seed, first, Posted(seed.FirstCommit, "coverage/project", StatusState.Success, "80.00% (+0.00%) compared to bbbbbbb", Now.AddMinutes(5), Url + "/2"));

    var status = (await ListAsync(seed, first, seed.FirstCommit)).ShouldHaveSingleItem();
    status.CommitId.ShouldBe(seed.FirstCommit);
    status.Status.Context.ShouldBe("coverage/project");
    status.Status.State.ShouldBe(StatusState.Success);
    status.Status.Description.ShouldBe("80.00% (+0.00%) compared to bbbbbbb");
    status.TargetUrl.ShouldBe(Url + "/2");
    status.PostedAt.ShouldBe(Now.AddMinutes(5));
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task ListByCommit_ShouldReturnEveryContext(DatabaseProvider provider)
  {
    var seed = await SeededAsync(provider);
    var first = seed.Organizations.FirstId;

    await UpsertAsync(seed, first, Posted(seed.FirstCommit, "coverage/project", StatusState.Success, "a", Now));
    await UpsertAsync(seed, first, Posted(seed.FirstCommit, "coverage/patch", StatusState.Failure, "b", Now));
    await UpsertAsync(seed, first, Posted(seed.FirstCommit, "coverage/changes", StatusState.Error, "c", Now));

    var statuses = await ListAsync(seed, first, seed.FirstCommit);

    statuses.Select(static s => (s.Status.Context, s.Status.State, s.Status.Description))
            .ShouldBe([("coverage/changes", StatusState.Error, "c"), ("coverage/patch", StatusState.Failure, "b"), ("coverage/project", StatusState.Success, "a")]);
  }

  // The context of a flag status ends with the flag, which is case-sensitive on both engines.
  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task Upsert_ContextsDifferingByCase_ShouldKeepTwoRows(DatabaseProvider provider)
  {
    var seed = await SeededAsync(provider);
    var first = seed.Organizations.FirstId;

    await using (var context = seed.Organizations.ContextOf(first))
    {
      // In one scope: the change tracker must tell them apart too.
      var repository = new PostedStatusRepository(context);

      await repository.UpsertAsync(Posted(seed.FirstCommit, "coverage/project/Backend", StatusState.Success, "upper", Now), Ct);
      await repository.UpsertAsync(Posted(seed.FirstCommit, "coverage/project/backend", StatusState.Failure, "lower", Now), Ct);
      await repository.UpsertAsync(Posted(seed.FirstCommit, "coverage/project/backend", StatusState.Failure, "lower again", Now), Ct);
    }

    var statuses = await ListAsync(seed, first, seed.FirstCommit);

    statuses.Select(static s => (s.Status.Context, s.Status.Description)).ShouldBe([("coverage/project/Backend", "upper"), ("coverage/project/backend", "lower again")]);
  }

  // The acceptance of M2.19a: a description of 140 characters, the longest a status holds, fits on both engines.
  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task Upsert_LongestDescriptionAndContext_ShouldBeStoredWhole(DatabaseProvider provider)
  {
    var seed = await SeededAsync(provider);
    var first = seed.Organizations.FirstId;
    var description = new string('é', CommitStatus.DescriptionMaxLength);
    var statusContext = "coverage/project/" + new string('f', PostedStatus.ContextMaxLength - "coverage/project/".Length);

    await UpsertAsync(seed, first, Posted(seed.FirstCommit, statusContext, StatusState.Success, description + "cut", Now));

    var status = (await ListAsync(seed, first, seed.FirstCommit)).ShouldHaveSingleItem();
    status.Status.Description.ShouldBe(description);
    status.Status.Context.ShouldBe(statusContext);
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task Upsert_WithoutTargetUrl_ShouldStoreNone(DatabaseProvider provider)
  {
    var seed = await SeededAsync(provider);
    var first = seed.Organizations.FirstId;

    await UpsertAsync(seed, first, new PostedStatus(seed.FirstCommit, new CommitStatus("coverage/project", StatusState.Success, "ok"), null, Now));

    (await ListAsync(seed, first, seed.FirstCommit)).ShouldHaveSingleItem().TargetUrl.ShouldBeNull();
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task ListByCommit_OtherOrganization_ShouldReturnNothing(DatabaseProvider provider)
  {
    var seed = await SeededAsync(provider);

    await UpsertAsync(seed, seed.Organizations.FirstId, Posted(seed.FirstCommit, "coverage/project", StatusState.Success, "first", Now));
    await UpsertAsync(seed, seed.Organizations.SecondId, Posted(seed.SecondCommit, "coverage/project", StatusState.Failure, "second", Now));

    (await ListAsync(seed, seed.Organizations.SecondId, seed.FirstCommit)).ShouldBeEmpty();
    (await ListAsync(seed, seed.Organizations.FirstId, seed.SecondCommit)).ShouldBeEmpty();
    (await ListAsync(seed, seed.Organizations.FirstId, seed.FirstCommit)).ShouldHaveSingleItem().Status.Description.ShouldBe("first");
    (await ListAsync(seed, seed.Organizations.SecondId, seed.SecondCommit)).ShouldHaveSingleItem().Status.Description.ShouldBe("second");
  }

  // The primary key does not hold the organization: a row for the commit of another one would take its key.
  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task Upsert_CommitOfAnotherOrganization_ShouldThrowConflictAndWriteNothing(DatabaseProvider provider)
  {
    var seed = await SeededAsync(provider);

    var exception = await Should.ThrowAsync<ConflictException>(
                      () => UpsertAsync(seed, seed.Organizations.FirstId, Posted(seed.SecondCommit, "coverage/project", StatusState.Success, "foreign", Now))
                    );

    exception.ErrorCode.ShouldBe(GeneralErrors.ConcurrentChange);
    (await ListAsync(seed, seed.Organizations.SecondId, seed.SecondCommit)).ShouldBeEmpty();
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task ListByCommit_WithoutOrganization_ShouldThrow(DatabaseProvider provider)
  {
    var seed = await SeededAsync(provider);
    await using var context = seed.Organizations.ContextWithoutOrganization();

    await Should.ThrowAsync<InvalidOperationException>(() => new PostedStatusRepository(context).ListByCommitAsync(seed.FirstCommit, Ct));
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task ListByCommit_NothingPublished_ShouldReturnNothing(DatabaseProvider provider)
  {
    var seed = await SeededAsync(provider);

    (await ListAsync(seed, seed.Organizations.FirstId, seed.FirstCommit)).ShouldBeEmpty();
  }

  // Two notifications of the same commit that overlap: one row per context, the one of the save that ends last.
  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task Upsert_TwoAtTheSameTime_ShouldLeaveOneRow(DatabaseProvider provider)
  {
    var seed = await SeededAsync(provider);

    // Held on its insert: the row does not exist yet for the other save, which inserts too and commits first.
    var holding = new HoldingCommandInterceptor(static sql => sql.Contains("INSERT", StringComparison.OrdinalIgnoreCase)
                                                             && sql.Contains("PostedStatuses", StringComparison.Ordinal));

    var holder = UpsertInItsOwnContextAsync(seed, provider, holding, "held");
    await holding.Reached.Task.WaitAsync(TimeSpan.FromMinutes(1), Ct);

    await UpsertInItsOwnContextAsync(seed, provider, null, "first to commit");

    holding.Gate.SetResult();
    await holder.WaitAsync(TimeSpan.FromMinutes(1), Ct);

    // The held save lost the insert, found the row and replaced it.
    (await ListAsync(seed, seed.Organizations.FirstId, seed.FirstCommit)).ShouldHaveSingleItem().Status.Description.ShouldBe("held");
  }

  [Fact]
  public async Task Upsert_Null_ShouldThrow()
  {
    await using var context = DatabaseFixture.CreateContext(Enum.GetValues<DatabaseProvider>()[0], "Server=localhost;Database=unused");

    await Should.ThrowAsync<ArgumentNullException>(() => new PostedStatusRepository(context).UpsertAsync(null!, Ct));
  }

  private static PostedStatus Posted(long commitId, string statusContext, StatusState state, string description, DateTimeOffset at, string url = Url) =>
    new(commitId, new CommitStatus(statusContext, state, description), url, at);

  private static async Task UpsertAsync(CommitSeed seed, long organizationId, PostedStatus status)
  {
    await using var context = seed.Organizations.ContextOf(organizationId);

    await new PostedStatusRepository(context).UpsertAsync(status, Ct);
  }

  private static async Task<IReadOnlyList<PostedStatus>> ListAsync(CommitSeed seed, long organizationId, long commitId)
  {
    await using var context = seed.Organizations.ContextOf(organizationId);

    return await new PostedStatusRepository(context).ListByCommitAsync(commitId, Ct);
  }

  private static async Task UpsertInItsOwnContextAsync(CommitSeed seed, DatabaseProvider provider, HoldingCommandInterceptor? interceptor, string description)
  {
    var options = new DbContextOptionsBuilder<CodeCoverageDbContext>();
    options.UseCodeCoverageDatabase(provider, seed.Organizations.ConnectionString);

    if (interceptor is not null)
    {
      options.AddInterceptors(interceptor);
    }

    var organization = new OrganizationContext();
    organization.Enter(seed.Organizations.FirstId);

    await using var context = new CodeCoverageDbContext(options.Options, organization);

    await new PostedStatusRepository(context).UpsertAsync(Posted(seed.FirstCommit, "coverage/project", StatusState.Success, description, Now), Ct);
  }

  private async Task<CommitSeed> SeededAsync(DatabaseProvider provider)
  {
    var repositories = await AddRepositoriesAsync(database, provider, Ct);

    await using var first = repositories.Organizations.ContextOf(repositories.Organizations.FirstId);
    await using var second = repositories.Organizations.ContextOf(repositories.Organizations.SecondId);
    var firstCommit = await new CommitRepository(first).GetOrCreateAsync(repositories.FirstRepository, Head, Now, Ct);
    var secondCommit = await new CommitRepository(second).GetOrCreateAsync(repositories.SecondRepository, Head, Now, Ct);

    return new CommitSeed(repositories.Organizations, repositories.FirstRepository, firstCommit.Id, secondCommit.Id);
  }

  #endregion
}