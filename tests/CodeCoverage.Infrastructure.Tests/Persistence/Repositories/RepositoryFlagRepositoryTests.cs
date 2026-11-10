// ------------------------------------------------------------------------------------------------
// <copyright file="RepositoryFlagRepositoryTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.EntityFrameworkCore;

using CodeCoverage.Application.Exceptions;
using CodeCoverage.Application.Features.Repositories;
using CodeCoverage.Domain.Commits;
using CodeCoverage.Infrastructure.Persistence;
using CodeCoverage.Infrastructure.Persistence.Repositories;
using CodeCoverage.Infrastructure.Tests.Support;

using static CodeCoverage.Infrastructure.Tests.Support.RepositorySeeder;

namespace CodeCoverage.Infrastructure.Tests.Persistence.Repositories;

// Every method against two organizations, on both engines: the rows of the other one are neither read nor written.
[Collection(DatabaseCollection.Name)]
public sealed class RepositoryFlagRepositoryTests(DatabaseFixture database)
{
  #region Fields

  private const int Workers = 8;

  #endregion

  #region Properties

  private static CancellationToken Ct => TestContext.Current.CancellationToken;

  #endregion

  #region Methods

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task AddMissing_ShouldRecordEachFlagOnce(DatabaseProvider provider)
  {
    var seed = await AddRepositoriesAsync(database, provider, Ct);
    var first = seed.Organizations.FirstId;

    await AddAsync(seed.Organizations, first, seed.FirstRepository, ["unit", "e2e", "unit"], Now);
    await AddAsync(seed.Organizations, first, seed.FirstRepository, ["e2e", "back"], Now.AddHours(1));

    (await ListAsync(seed.Organizations, first, seed.FirstRepository)).ShouldBe(["back", "e2e", "unit"]);

    // A flag keeps the moment it was first seen.
    await using var context = seed.Organizations.ContextOf(first);
    var rows = await context.RepositoryFlags.AsNoTracking().ToDictionaryAsync(static f => f.Flag, static f => f.CreatedAt, Ct);
    rows["unit"].ShouldBe(Now);
    rows["e2e"].ShouldBe(Now);
    rows["back"].ShouldBe(Now.AddHours(1));
  }

  // A flag is case-sensitive on both engines, and listed in ordinal order whatever the collation of the database.
  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task AddMissing_FlagsThatDifferByTheirCase_ShouldBeTwoFlags(DatabaseProvider provider)
  {
    var seed = await AddRepositoriesAsync(database, provider, Ct);
    var first = seed.Organizations.FirstId;

    await AddAsync(seed.Organizations, first, seed.FirstRepository, ["unit", "Unit", "UNIT", "a", "B"], Now);
    await AddAsync(seed.Organizations, first, seed.FirstRepository, ["Unit"], Now);

    (await ListAsync(seed.Organizations, first, seed.FirstRepository)).ShouldBe(["B", "UNIT", "Unit", "a", "unit"]);
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task AddMissing_LongestFlag_ShouldBeStored(DatabaseProvider provider)
  {
    var seed = await AddRepositoriesAsync(database, provider, Ct);
    var flag = new string('f', CommitFlagTotals.FlagMaxLength);

    await AddAsync(seed.Organizations, seed.Organizations.FirstId, seed.FirstRepository, [flag], Now);

    (await ListAsync(seed.Organizations, seed.Organizations.FirstId, seed.FirstRepository)).ShouldBe([flag]);
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task AddMissing_NoFlag_ShouldWriteNothing(DatabaseProvider provider)
  {
    var seed = await AddRepositoriesAsync(database, provider, Ct);

    await AddAsync(seed.Organizations, seed.Organizations.FirstId, seed.FirstRepository, [], Now);

    (await ListAsync(seed.Organizations, seed.Organizations.FirstId, seed.FirstRepository)).ShouldBeEmpty();
  }

  // The same flag in two repositories, one per organization: each one only sees its own.
  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task List_ShouldOnlyReturnTheFlagsOfTheRepositoryOfTheOrganization(DatabaseProvider provider)
  {
    var seed = await AddRepositoriesAsync(database, provider, Ct);
    var organizations = seed.Organizations;

    await AddAsync(organizations, organizations.FirstId, seed.FirstRepository, ["unit", "first"], Now);
    await AddAsync(organizations, organizations.SecondId, seed.SecondRepository, ["unit", "second"], Now);

    (await ListAsync(organizations, organizations.FirstId, seed.FirstRepository)).ShouldBe(["first", "unit"]);
    (await ListAsync(organizations, organizations.SecondId, seed.SecondRepository)).ShouldBe(["second", "unit"]);

    // Asked from the other organization: the repository does not exist there.
    (await ListAsync(organizations, organizations.FirstId, seed.SecondRepository)).ShouldBeEmpty();
    (await ListAsync(organizations, organizations.SecondId, seed.FirstRepository)).ShouldBeEmpty();
  }

  // The key does not hold the organization: a flag written for the repository of another one would take its key.
  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task AddMissing_RepositoryOfAnotherOrganization_ShouldThrowNotFoundAndWriteNothing(DatabaseProvider provider)
  {
    var seed = await AddRepositoriesAsync(database, provider, Ct);
    var organizations = seed.Organizations;

    var exception = await Should.ThrowAsync<NotFoundException>(() => AddAsync(organizations, organizations.FirstId, seed.SecondRepository, ["unit"], Now));

    exception.ErrorCode.ShouldBe(RepositoryErrors.NotFound);

    await using var check = organizations.ContextWithoutOrganization();
    (await check.RepositoryFlags.IgnoreQueryFilters().CountAsync(Ct)).ShouldBe(0);

    // The flag is still free for the organization the repository belongs to.
    await AddAsync(organizations, organizations.SecondId, seed.SecondRepository, ["unit"], Now);
    (await ListAsync(organizations, organizations.SecondId, seed.SecondRepository)).ShouldBe(["unit"]);
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task AddMissing_UnknownRepository_ShouldThrowNotFound(DatabaseProvider provider)
  {
    var seed = await AddRepositoriesAsync(database, provider, Ct);

    await Should.ThrowAsync<NotFoundException>(() => AddAsync(seed.Organizations, seed.Organizations.FirstId, long.MaxValue, ["unit"], Now));
  }

  // The collision itself, without any timing: the flag was recorded by another finalization after this one read the
  // flags of the repository. The insertion reaches the primary key, is refused, and leaves the scope usable.
  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task Insert_FlagRecordedByAnotherScopeMeanwhile_ShouldBeAbsorbedAndLeaveTheScopeUsable(DatabaseProvider provider)
  {
    var seed = await AddRepositoriesAsync(database, provider, Ct);
    var first = seed.Organizations.FirstId;

    await using var late = seed.Organizations.ContextOf(first);
    var repository = new RepositoryFlagRepository(late);
    (await repository.ListAsync(seed.FirstRepository, Ct)).ShouldBeEmpty();

    await AddAsync(seed.Organizations, first, seed.FirstRepository, ["unit"], Now);

    await repository.InsertAsync(seed.FirstRepository, "unit", Now.AddHours(1), Ct);

    late.ChangeTracker.Entries().ShouldBeEmpty();
    await repository.AddMissingAsync(seed.FirstRepository, ["unit", "e2e"], Now.AddHours(2), Ct);
    (await repository.ListAsync(seed.FirstRepository, Ct)).ShouldBe(["e2e", "unit"]);

    // The first writer won: its moment is kept.
    (await late.RepositoryFlags.AsNoTracking().SingleAsync(static f => f.Flag == "unit", Ct)).CreatedAt.ShouldBe(Now);
  }

  // Several finalizations of one repository record the same new flags at once: none fails, each flag is stored once.
  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task AddMissing_Concurrent_ShouldRecordEachFlagOnceAndNeverFail(DatabaseProvider provider)
  {
    var seed = await AddRepositoriesAsync(database, provider, Ct);
    var organizations = seed.Organizations;
    var go = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var arrived = 0;

    var workers = Enumerable.Range(0, Workers)
                            .Select(
                              worker => Task.Run(
                                async () =>
                                {
                                  await using var context = organizations.ContextOf(organizations.FirstId);
                                  await context.Database.OpenConnectionAsync(Ct);

                                  if (Interlocked.Increment(ref arrived) == Workers)
                                  {
                                    go.TrySetResult();
                                  }

                                  await go.Task.WaitAsync(Ct);

                                  // Insertions only, so that every worker but one reaches the primary key whatever the timing.
                                  var repository = new RepositoryFlagRepository(context);
                                  await repository.InsertAsync(seed.FirstRepository, "unit", Now.AddMinutes(worker), Ct);
                                  await repository.InsertAsync(seed.FirstRepository, "e2e", Now.AddMinutes(worker), Ct);
                                  await repository.AddMissingAsync(seed.FirstRepository, ["unit", "e2e", "back"], Now.AddMinutes(worker), Ct);
                                },
                                Ct
                              )
                            )
                            .ToArray();

    await Task.WhenAll(workers);

    (await ListAsync(organizations, organizations.FirstId, seed.FirstRepository)).ShouldBe(["back", "e2e", "unit"]);

    await using var check = organizations.ContextWithoutOrganization();
    (await check.RepositoryFlags.IgnoreQueryFilters().CountAsync(Ct)).ShouldBe(3);
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task AddMissing_NullFlags_ShouldThrow(DatabaseProvider provider)
  {
    var seed = await AddRepositoriesAsync(database, provider, Ct);

    await using var context = seed.Organizations.ContextOf(seed.Organizations.FirstId);

    await Should.ThrowAsync<ArgumentNullException>(() => new RepositoryFlagRepository(context).AddMissingAsync(seed.FirstRepository, null!, Now, Ct));
  }

  private static async Task AddAsync(TwoOrganizationDatabase organizations, long organizationId, long repositoryId, string[] flags, DateTimeOffset at)
  {
    await using var context = organizations.ContextOf(organizationId);

    await new RepositoryFlagRepository(context).AddMissingAsync(repositoryId, flags, at, Ct);
  }

  private static async Task<IReadOnlyList<string>> ListAsync(TwoOrganizationDatabase organizations, long organizationId, long repositoryId)
  {
    await using var context = organizations.ContextOf(organizationId);

    return await new RepositoryFlagRepository(context).ListAsync(repositoryId, Ct);
  }

  #endregion
}
