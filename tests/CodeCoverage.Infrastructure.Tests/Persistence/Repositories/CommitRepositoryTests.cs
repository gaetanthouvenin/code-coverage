// ------------------------------------------------------------------------------------------------
// <copyright file="CommitRepositoryTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Data.Common;

using Microsoft.EntityFrameworkCore;

using CodeCoverage.Application.Exceptions;
using CodeCoverage.Application.Exceptions.ErrorCodes;
using CodeCoverage.Application.Features.Repositories;
using CodeCoverage.Domain.Commits;
using CodeCoverage.Domain.Git;
using CodeCoverage.Domain.Repositories;
using CodeCoverage.Infrastructure.Persistence;
using CodeCoverage.Infrastructure.Persistence.Repositories;
using CodeCoverage.Infrastructure.Tests.Support;

using static CodeCoverage.Infrastructure.Tests.Support.RepositorySeeder;

namespace CodeCoverage.Infrastructure.Tests.Persistence.Repositories;

// Every method against two organizations, on both engines. The races use one context, hence one connection,
// per worker: nothing but the database orders them.
[Collection(DatabaseCollection.Name)]
public sealed class CommitRepositoryTests(DatabaseFixture database)
{
  #region Fields

  private const int Workers = 16;

  #endregion

  #region Properties

  private static CancellationToken Ct => TestContext.Current.CancellationToken;

  #endregion

  #region Methods

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task GetOrCreate_New_ShouldAddAKnownCommit(DatabaseProvider provider)
  {
    var seed = await AddRepositoriesAsync(database, provider, Ct);
    await using var context = seed.Organizations.ContextOf(seed.Organizations.FirstId);

    var commit = await new CommitRepository(context).GetOrCreateAsync(seed.FirstRepository, Head, Now, Ct);

    commit.Id.ShouldBePositive();
    commit.State.ShouldBe(CommitState.Known);

    await using var reader = seed.Organizations.ContextOf(seed.Organizations.FirstId);
    (await new CommitRepository(reader).FindByShaAsync(seed.FirstRepository, Head, Ct)).ShouldNotBeNull().Id.ShouldBe(commit.Id);
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task GetOrCreate_Existing_ShouldReturnTheRowAndLeaveTheScopeUsable(DatabaseProvider provider)
  {
    var seed = await AddRepositoriesAsync(database, provider, Ct);
    var first = await GetOrCreateAsync(seed, seed.Organizations.FirstId, seed.FirstRepository);

    await using var context = seed.Organizations.ContextOf(seed.Organizations.FirstId);
    var commits = new CommitRepository(context);

    var again = await commits.GetOrCreateAsync(seed.FirstRepository, Head, Now.AddHours(1), Ct);
    again.MarkPending("main", null, Now.AddHours(1));
    await commits.UpdateAsync(again, Ct);

    again.Id.ShouldBe(first);
    again.CreatedAt.ShouldBe(Now);
  }

  // Inserting first, every worker but one reaches the unique index whatever the timing: the race cannot be missed.
  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task GetOrCreate_Concurrent_ShouldCreateOnce(DatabaseProvider provider)
  {
    var seed = await AddRepositoriesAsync(database, provider, Ct);

    var commits = await RaceAsync(
      seed.Organizations,
      seed.Organizations.FirstId,
      context => new CommitRepository(context).GetOrCreateAsync(seed.FirstRepository, Head, Now, Ct)
    );

    commits.Select(static c => c.Id).Distinct().ShouldHaveSingleItem();

    await using var reader = seed.Organizations.ContextOf(seed.Organizations.FirstId);
    (await reader.Commits.CountAsync(Ct)).ShouldBe(1);
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task GetOrCreate_SameShaInTwoOrganizations_ShouldCreateTwoCommits(DatabaseProvider provider)
  {
    var seed = await AddRepositoriesAsync(database, provider, Ct);

    var first = await GetOrCreateAsync(seed, seed.Organizations.FirstId, seed.FirstRepository);
    var second = await GetOrCreateAsync(seed, seed.Organizations.SecondId, seed.SecondRepository);

    first.ShouldNotBe(second);

    await using var firstContext = seed.Organizations.ContextOf(seed.Organizations.FirstId);
    (await firstContext.Commits.SingleAsync(Ct)).Id.ShouldBe(first);

    await using var secondContext = seed.Organizations.ContextOf(seed.Organizations.SecondId);
    (await secondContext.Commits.SingleAsync(Ct)).Id.ShouldBe(second);
  }

  // The unique index does not hold the organization: without the check, the SHA would be taken in the other one.
  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task GetOrCreate_RepositoryOfOtherOrganization_ShouldThrowNotFoundAndWriteNothing(DatabaseProvider provider)
  {
    var seed = await AddRepositoriesAsync(database, provider, Ct);

    await using (var first = seed.Organizations.ContextOf(seed.Organizations.FirstId))
    {
      var exception = await Should.ThrowAsync<NotFoundException>(
        () => new CommitRepository(first).GetOrCreateAsync(seed.SecondRepository, Head, Now, Ct)
      );

      exception.ErrorCode.ShouldBe(RepositoryErrors.NotFound);
    }

    await using var second = seed.Organizations.ContextOf(seed.Organizations.SecondId);
    (await second.Commits.CountAsync(Ct)).ShouldBe(0);
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task GetOrCreate_UnknownRepository_ShouldThrowNotFound(DatabaseProvider provider)
  {
    var seed = await AddRepositoriesAsync(database, provider, Ct);
    await using var first = seed.Organizations.ContextOf(seed.Organizations.FirstId);

    var exception = await Should.ThrowAsync<NotFoundException>(
      () => new CommitRepository(first).GetOrCreateAsync(long.MaxValue, Head, Now, Ct)
    );

    exception.ErrorCode.ShouldBe(RepositoryErrors.NotFound);
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task Find_OtherOrganization_ShouldReturnNull(DatabaseProvider provider)
  {
    var seed = await AddRepositoriesAsync(database, provider, Ct);
    var foreign = await GetOrCreateAsync(seed, seed.Organizations.SecondId, seed.SecondRepository);

    await using var first = seed.Organizations.ContextOf(seed.Organizations.FirstId);

    (await new CommitRepository(first).FindAsync(foreign, Ct)).ShouldBeNull();
  }

  // The sweep of M2.23: finalized, never notified, nothing in progress, in a tracked repository, within the window.
  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task ListStuck_ShouldKeepOnlyTheCommitsNothingWillEverNotify(DatabaseProvider provider)
  {
    var seed = await AddRepositoriesAsync(database, provider, Ct);
    var first = seed.Organizations.FirstId;
    long inactive;

    await using (var setup = seed.Organizations.ContextOf(first))
    {
      inactive = await AddRepositoryAsync(setup, FirstInstallation, "77", "first-org/gone", Ct);
      (await setup.Repositories.SingleAsync(r => r.Id == inactive, Ct)).Deactivate(Now);
      await setup.SaveChangesAsync(Ct);
    }

    var stuck = await AddCommitAsync(seed, first, seed.FirstRepository, 1, CommitState.Complete, Now.AddHours(-3));
    var inError = await AddCommitAsync(seed, first, seed.FirstRepository, 2, CommitState.Error, Now.AddHours(-2));
    await AddCommitAsync(seed, first, seed.FirstRepository, 3, CommitState.Complete, Now.AddHours(-3), notified: true);
    await AddCommitAsync(seed, first, seed.FirstRepository, 4, CommitState.Complete, Now.AddMinutes(-10));
    await AddCommitAsync(seed, first, seed.FirstRepository, 5, CommitState.Pending, Now.AddHours(-3));
    await AddCommitAsync(seed, first, seed.FirstRepository, 6, CommitState.Complete, Now.AddHours(-3), uploadInProgress: UploadState.Received);
    await AddCommitAsync(seed, first, seed.FirstRepository, 7, CommitState.Complete, Now.AddHours(-3), uploadInProgress: UploadState.Processing);
    await AddCommitAsync(seed, first, seed.FirstRepository, 8, CommitState.Complete, Now.AddDays(-10));
    await AddCommitAsync(seed, first, seed.FirstRepository, 9, CommitState.Known, null);
    await AddCommitAsync(seed, first, inactive, 10, CommitState.Complete, Now.AddHours(-3));
    var other = await AddCommitAsync(seed, seed.Organizations.SecondId, seed.SecondRepository, 11, CommitState.Complete, Now.AddHours(-3));

    await using var context = seed.Organizations.ContextOf(first);
    var repository = new CommitRepository(context);

    var listed = await repository.ListStuckAsync(Now.AddDays(-7), Now.AddHours(-1), null, 50, Ct);

    // The oldest last upload first.
    listed.Select(static c => c.Id).ShouldBe([stuck, inError]);
    context.ChangeTracker.Entries().ShouldBeEmpty();
    (await repository.ListStuckAsync(Now.AddDays(-7), Now.AddHours(-1), null, 1, Ct)).ShouldHaveSingleItem().Id.ShouldBe(stuck);

    // The bounds: the oldest one is included, the newest one is not.
    (await repository.ListStuckAsync(Now.AddHours(-3), Now.AddHours(-2), null, 50, Ct)).ShouldHaveSingleItem().Id.ShouldBe(stuck);

    await using var second = seed.Organizations.ContextOf(seed.Organizations.SecondId);
    (await new CommitRepository(second).ListStuckAsync(Now.AddDays(-7), Now.AddHours(-1), null, 50, Ct)).ShouldHaveSingleItem().Id.ShouldBe(other);
  }

  // M3.07: a repository an administrator disabled keeps its row and its state, and the sweep leaves its commits out.
  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task ListStuck_DisabledRepository_ShouldLeaveItsCommitsOut(DatabaseProvider provider)
  {
    var seed = await AddRepositoriesAsync(database, provider, Ct);
    var first = seed.Organizations.FirstId;
    long disabled;

    await using (var setup = seed.Organizations.ContextOf(first))
    {
      disabled = await AddRepositoryAsync(setup, FirstInstallation, "78", "first-org/disabled", Ct);
      (await setup.Repositories.SingleAsync(r => r.Id == disabled, Ct)).Disable(Now);
      await setup.SaveChangesAsync(Ct);
    }

    var stuck = await AddCommitAsync(seed, first, seed.FirstRepository, 1, CommitState.Complete, Now.AddHours(-3));
    await AddCommitAsync(seed, first, disabled, 2, CommitState.Complete, Now.AddHours(-3));

    await using var context = seed.Organizations.ContextOf(first);
    var read = await context.Repositories.AsNoTracking().SingleAsync(r => r.Id == disabled, Ct);

    read.DisabledAt.ShouldBe(Now);
    read.IsActive.ShouldBeTrue();
    (await new CommitRepository(context).ListStuckAsync(Now.AddDays(-7), Now.AddHours(-1), null, 50, Ct)).ShouldHaveSingleItem().Id.ShouldBe(stuck);
  }

  // F5. Pages after a commit, in the same order: commits with the same last upload are told apart by their identifier.
  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task ListStuck_AfterACommit_ShouldGoOnInTheSameOrder(DatabaseProvider provider)
  {
    var seed = await AddRepositoriesAsync(database, provider, Ct);
    var first = seed.Organizations.FirstId;
    var a = await AddCommitAsync(seed, first, seed.FirstRepository, 1, CommitState.Complete, Now.AddHours(-3));
    var b = await AddCommitAsync(seed, first, seed.FirstRepository, 2, CommitState.Complete, Now.AddHours(-3));
    var c = await AddCommitAsync(seed, first, seed.FirstRepository, 3, CommitState.Complete, Now.AddHours(-3));
    var d = await AddCommitAsync(seed, first, seed.FirstRepository, 4, CommitState.Error, Now.AddHours(-2));

    await using var context = seed.Organizations.ContextOf(first);
    var repository = new CommitRepository(context);
    var read = new List<long>();
    Commit? after = null;

    while (true)
    {
      var page = await repository.ListStuckAsync(Now.AddDays(-7), Now.AddHours(-1), after, 2, Ct);

      read.AddRange(page.Select(static commit => commit.Id));

      if (page.Count < 2)
      {
        break;
      }

      after = page[^1];
    }

    read.ShouldBe([a, b, c, d]);
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task ListStuck_InvalidLimit_ShouldThrow(DatabaseProvider provider)
  {
    var seed = await AddRepositoriesAsync(database, provider, Ct);
    await using var context = seed.Organizations.ContextOf(seed.Organizations.FirstId);

    await Should.ThrowAsync<ArgumentOutOfRangeException>(() => new CommitRepository(context).ListStuckAsync(Now, Now, null, 0, Ct));
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task ListByShas_ShouldGiveTheCommitsFoundWithoutAttachingThem(DatabaseProvider provider)
  {
    var seed = await AddRepositoriesAsync(database, provider, Ct);
    var known = await GetOrCreateAsync(seed, seed.Organizations.FirstId, seed.FirstRepository);
    await using var context = seed.Organizations.ContextOf(seed.Organizations.FirstId);

    var found = await new CommitRepository(context).ListByShasAsync(seed.FirstRepository, [Head, NewHead], Ct);

    found.ShouldHaveSingleItem().Id.ShouldBe(known);
    context.ChangeTracker.Entries().ShouldBeEmpty();
    (await new CommitRepository(context).ListByShasAsync(seed.FirstRepository, [], Ct)).ShouldBeEmpty();
    (await new CommitRepository(context).ListByShasAsync(seed.FirstRepository, [NewHead], Ct)).ShouldBeEmpty();
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task ListByShas_OtherOrganization_ShouldGiveNothing(DatabaseProvider provider)
  {
    var seed = await AddRepositoriesAsync(database, provider, Ct);
    await GetOrCreateAsync(seed, seed.Organizations.SecondId, seed.SecondRepository);

    await using var first = seed.Organizations.ContextOf(seed.Organizations.FirstId);

    (await new CommitRepository(first).ListByShasAsync(seed.SecondRepository, [Head], Ct)).ShouldBeEmpty();
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task FindBySha_OtherOrganization_ShouldReturnNull(DatabaseProvider provider)
  {
    var seed = await AddRepositoriesAsync(database, provider, Ct);
    await GetOrCreateAsync(seed, seed.Organizations.SecondId, seed.SecondRepository);

    await using var first = seed.Organizations.ContextOf(seed.Organizations.FirstId);

    (await new CommitRepository(first).FindByShaAsync(seed.SecondRepository, Head, Ct)).ShouldBeNull();
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task FindBySha_InvalidStoredSha_ShouldThrow(DatabaseProvider provider)
  {
    var seed = await AddRepositoriesAsync(database, provider, Ct);
    await GetOrCreateAsync(seed, seed.Organizations.FirstId, seed.FirstRepository);
    await using var context = seed.Organizations.ContextOf(seed.Organizations.FirstId);
    await context.Database.ExecuteSqlAsync($"""UPDATE "Commits" SET "Sha" = {"not-a-sha"}""", Ct);

    await Should.ThrowAsync<ArgumentException>(() => context.Commits.SingleAsync(Ct));
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task NextUploadSequence_ShouldCountFromOne(DatabaseProvider provider)
  {
    var seed = await AddRepositoriesAsync(database, provider, Ct);
    var id = await GetOrCreateAsync(seed, seed.Organizations.FirstId, seed.FirstRepository);
    await using var context = seed.Organizations.ContextOf(seed.Organizations.FirstId);
    var commits = new CommitRepository(context);

    (await commits.NextUploadSequenceAsync(id, Ct)).ShouldBe(1);
    (await commits.NextUploadSequenceAsync(id, Ct)).ShouldBe(2);
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task NextUploadSequence_Concurrent_ShouldBeUnique(DatabaseProvider provider)
  {
    var seed = await AddRepositoriesAsync(database, provider, Ct);
    var id = await GetOrCreateAsync(seed, seed.Organizations.FirstId, seed.FirstRepository);

    var sequences = await RaceAsync(
      seed.Organizations,
      seed.Organizations.FirstId,
      context => new CommitRepository(context).NextUploadSequenceAsync(id, Ct)
    );

    sequences.Order().ShouldBe(Enumerable.Range(1, Workers));

    await using var reader = seed.Organizations.ContextOf(seed.Organizations.FirstId);
    (await new CommitRepository(reader).FindAsync(id, Ct)).ShouldNotBeNull().UploadSequence.ShouldBe(Workers);
  }

  // The proof that the increment and its reading are one step for the others. The delay only sets how much the
  // test proves: a loaded machine can shorten what it observes, never make a correct implementation fail it.
  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task NextUploadSequence_WhileAnotherScopeHoldsTheRow_ShouldWaitForIt(DatabaseProvider provider)
  {
    var seed = await AddRepositoriesAsync(database, provider, Ct);
    var id = await GetOrCreateAsync(seed, seed.Organizations.FirstId, seed.FirstRepository);
    await using var holder = seed.Organizations.ContextOf(seed.Organizations.FirstId);
    await using var waiter = seed.Organizations.ContextOf(seed.Organizations.FirstId);
    await using var transaction = await holder.Database.BeginTransactionAsync(Ct);

    (await new CommitRepository(holder).NextUploadSequenceAsync(id, Ct)).ShouldBe(1);

    var waiting = new CommitRepository(waiter).NextUploadSequenceAsync(id, Ct);
    await Task.Delay(TimeSpan.FromMilliseconds(500), Ct);

    waiting.IsCompleted.ShouldBeFalse();

    await transaction.CommitAsync(Ct);

    (await waiting.WaitAsync(TimeSpan.FromMinutes(2), Ct)).ShouldBe(2);
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task NextUploadSequence_TransactionRolledBack_ShouldGiveTheNumberAgain(DatabaseProvider provider)
  {
    var seed = await AddRepositoriesAsync(database, provider, Ct);
    var id = await GetOrCreateAsync(seed, seed.Organizations.FirstId, seed.FirstRepository);
    await using var context = seed.Organizations.ContextOf(seed.Organizations.FirstId);
    var commits = new CommitRepository(context);

    await using (var transaction = await context.Database.BeginTransactionAsync(Ct))
    {
      (await commits.NextUploadSequenceAsync(id, Ct)).ShouldBe(1);
      await transaction.RollbackAsync(Ct);
    }

    (await commits.NextUploadSequenceAsync(id, Ct)).ShouldBe(1);
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task NextUploadSequence_OtherOrganization_ShouldThrowConflictAndLeaveTheRow(DatabaseProvider provider)
  {
    var seed = await AddRepositoriesAsync(database, provider, Ct);
    var foreign = await GetOrCreateAsync(seed, seed.Organizations.SecondId, seed.SecondRepository);

    await using (var first = seed.Organizations.ContextOf(seed.Organizations.FirstId))
    {
      var exception = await Should.ThrowAsync<ConflictException>(() => new CommitRepository(first).NextUploadSequenceAsync(foreign, Ct));

      exception.ErrorCode.ShouldBe(GeneralErrors.ConcurrentChange);
    }

    await using var second = seed.Organizations.ContextOf(seed.Organizations.SecondId);
    (await new CommitRepository(second).FindAsync(foreign, Ct)).ShouldNotBeNull().UploadSequence.ShouldBe(0);
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task NextUploadSequence_Unknown_ShouldThrowConflict(DatabaseProvider provider)
  {
    var seed = await AddRepositoriesAsync(database, provider, Ct);
    await using var first = seed.Organizations.ContextOf(seed.Organizations.FirstId);

    var exception = await Should.ThrowAsync<ConflictException>(() => new CommitRepository(first).NextUploadSequenceAsync(long.MaxValue, Ct));

    exception.ErrorCode.ShouldBe(GeneralErrors.ConcurrentChange);
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task Update_ReadInThisScope_ShouldSave(DatabaseProvider provider)
  {
    var seed = await AddRepositoriesAsync(database, provider, Ct);
    var id = await GetOrCreateAsync(seed, seed.Organizations.FirstId, seed.FirstRepository);

    await using (var context = seed.Organizations.ContextOf(seed.Organizations.FirstId))
    {
      var commits = new CommitRepository(context);
      var commit = (await commits.FindAsync(id, Ct)).ShouldNotBeNull();
      commit.MarkPending("main", 7, Now.AddHours(1));

      await commits.UpdateAsync(commit, Ct);
    }

    await using var reader = seed.Organizations.ContextOf(seed.Organizations.FirstId);
    var saved = (await new CommitRepository(reader).FindAsync(id, Ct)).ShouldNotBeNull();
    saved.State.ShouldBe(CommitState.Pending);
    saved.Branch.ShouldBe("main");
    saved.PullNumber.ShouldBe(7);
    saved.LastUploadAt.ShouldBe(Now.AddHours(1));
  }

  // Two uploads of a matrix read the commit together and both record themselves: neither is refused.
  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task Update_TwoUploadsMarkingPending_ShouldAbsorbBoth(DatabaseProvider provider)
  {
    var seed = await AddRepositoriesAsync(database, provider, Ct);
    var id = await GetOrCreateAsync(seed, seed.Organizations.FirstId, seed.FirstRepository);
    await using var firstUpload = seed.Organizations.ContextOf(seed.Organizations.FirstId);
    await using var secondUpload = seed.Organizations.ContextOf(seed.Organizations.FirstId);
    var firstCommits = new CommitRepository(firstUpload);
    var secondCommits = new CommitRepository(secondUpload);
    var firstRow = (await firstCommits.FindAsync(id, Ct)).ShouldNotBeNull();
    var secondRow = (await secondCommits.FindAsync(id, Ct)).ShouldNotBeNull();
    firstRow.MarkPending("main", null, Now.AddHours(1));
    secondRow.MarkPending("main", null, Now.AddHours(2));

    await firstCommits.UpdateAsync(firstRow, Ct);
    await secondCommits.UpdateAsync(secondRow, Ct);

    await using var reader = seed.Organizations.ContextOf(seed.Organizations.FirstId);
    (await new CommitRepository(reader).FindAsync(id, Ct)).ShouldNotBeNull().LastUploadAt.ShouldBe(Now.AddHours(2));
  }

  // The session numbers are written outside the tracker: a later save of a stale instance must not put one back.
  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task Update_AfterNextUploadSequence_ShouldKeepTheSequence(DatabaseProvider provider)
  {
    var seed = await AddRepositoriesAsync(database, provider, Ct);
    var id = await GetOrCreateAsync(seed, seed.Organizations.FirstId, seed.FirstRepository);

    await using (var context = seed.Organizations.ContextOf(seed.Organizations.FirstId))
    {
      var commits = new CommitRepository(context);
      var commit = (await commits.FindAsync(id, Ct)).ShouldNotBeNull();
      await commits.NextUploadSequenceAsync(id, Ct);
      commit.MarkPending("main", null, Now.AddHours(1));

      await commits.UpdateAsync(commit, Ct);
    }

    await using var reader = seed.Organizations.ContextOf(seed.Organizations.FirstId);
    (await new CommitRepository(reader).FindAsync(id, Ct)).ShouldNotBeNull().UploadSequence.ShouldBe(1);
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task Update_StaleVersion_ShouldThrowConflict(DatabaseProvider provider)
  {
    var seed = await AddRepositoriesAsync(database, provider, Ct);
    var id = await GetOrCreateAsync(seed, seed.Organizations.FirstId, seed.FirstRepository);
    await using var context = seed.Organizations.ContextOf(seed.Organizations.FirstId);
    var commits = new CommitRepository(context);
    var commit = (await commits.FindAsync(id, Ct)).ShouldNotBeNull();
    await context.Database.ExecuteSqlAsync($"""UPDATE "Commits" SET "Version" = {1}""", Ct);
    commit.MarkPending("main", null, Now.AddHours(1));

    var exception = await Should.ThrowAsync<ConflictException>(() => commits.UpdateAsync(commit, Ct));

    exception.ErrorCode.ShouldBe(GeneralErrors.ConcurrentChange);
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task Update_InstanceReadByOtherOrganization_ShouldThrowAndWriteNothing(DatabaseProvider provider)
  {
    var seed = await AddRepositoriesAsync(database, provider, Ct);
    var id = await GetOrCreateAsync(seed, seed.Organizations.SecondId, seed.SecondRepository);

    await using (var second = seed.Organizations.ContextOf(seed.Organizations.SecondId))
    {
      var foreign = (await new CommitRepository(second).FindAsync(id, Ct)).ShouldNotBeNull();
      foreign.MarkPending("main", null, Now.AddHours(1));

      await using var first = seed.Organizations.ContextOf(seed.Organizations.FirstId);

      await Should.ThrowAsync<InvalidOperationException>(() => new CommitRepository(first).UpdateAsync(foreign, Ct));
    }

    await using var reader = seed.Organizations.ContextOf(seed.Organizations.SecondId);
    (await new CommitRepository(reader).FindAsync(id, Ct)).ShouldNotBeNull().State.ShouldBe(CommitState.Known);
  }

  // An upload reads the commit, a finalization saves it, then the upload saves its reception: a matrix upload racing the
  // finalization of the commit must not be refused.
  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task SavePending_FinalizationSavedSinceTheRead_ShouldKeepBoth(DatabaseProvider provider)
  {
    var seed = await AddRepositoriesAsync(database, provider, Ct);
    var first = seed.Organizations.FirstId;

    await using (var setup = seed.Organizations.ContextOf(first))
    {
      var created = await new CommitRepository(setup).GetOrCreateAsync(seed.FirstRepository, Head, Now, Ct);
      created.MarkPending(null, null, Now);
      await setup.SaveChangesAsync(Ct);
    }

    await using var context = seed.Organizations.ContextOf(first);
    var repository = new CommitRepository(context);
    var commit = await repository.GetOrCreateAsync(seed.FirstRepository, Head, Now, Ct);

    await using (var finalizer = seed.Organizations.ContextOf(first))
    {
      var finalized = await new CommitRepository(finalizer).FindAsync(commit.Id, Ct);
      finalized!.RecordFinalization(CommitState.Complete, 0, default, 0, 0, CarryforwardStatus.None, "source", Now);
      await finalizer.SaveChangesAsync(Ct);
    }

    commit.MarkPending("feature", 12, Now.AddMinutes(1));
    await repository.SavePendingAsync(commit, Ct);

    await using var reader = seed.Organizations.ContextOf(first);
    var stored = await reader.Commits.AsNoTracking().SingleAsync(c => c.Id == commit.Id, Ct);
    stored.State.ShouldBe(CommitState.Complete);
    stored.ReportRevision.ShouldBe(1);
    stored.LastUploadAt.ShouldBe(Now.AddMinutes(1));
    stored.Branch.ShouldBe("feature");
    stored.PullNumber.ShouldBe(12);
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task SavePending_InstanceReadByOtherOrganization_ShouldThrowAndWriteNothing(DatabaseProvider provider)
  {
    var seed = await AddRepositoriesAsync(database, provider, Ct);
    var id = await GetOrCreateAsync(seed, seed.Organizations.SecondId, seed.SecondRepository);

    await using (var second = seed.Organizations.ContextOf(seed.Organizations.SecondId))
    {
      var foreign = (await new CommitRepository(second).FindAsync(id, Ct)).ShouldNotBeNull();
      foreign.MarkPending("main", null, Now.AddHours(1));

      await using var first = seed.Organizations.ContextOf(seed.Organizations.FirstId);

      await Should.ThrowAsync<InvalidOperationException>(() => new CommitRepository(first).SavePendingAsync(foreign, Ct));
    }

    await using var reader = seed.Organizations.ContextOf(seed.Organizations.SecondId);
    (await new CommitRepository(reader).FindAsync(id, Ct)).ShouldNotBeNull().State.ShouldBe(CommitState.Known);
  }

  // Raw on purpose: the Domain has nothing yet that sets the covered commit of a branch.
  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task Branches_CoveredCommit_ShouldBeHeldByTheForeignKey(DatabaseProvider provider)
  {
    var seed = await AddRepositoriesAsync(database, provider, Ct);
    var id = await GetOrCreateAsync(seed, seed.Organizations.FirstId, seed.FirstRepository);
    await using var context = seed.Organizations.ContextOf(seed.Organizations.FirstId);
    await new BranchRepository(context).AddAsync(Branch.Create(seed.FirstRepository, "main", Head, Now), Ct);

    await Should.ThrowAsync<DbException>(() => context.Database.ExecuteSqlAsync($"""UPDATE "Branches" SET "CoveredCommitId" = {long.MaxValue}""", Ct));

    await context.Database.ExecuteSqlAsync($"""UPDATE "Branches" SET "CoveredCommitId" = {id}""", Ct);

    await Should.ThrowAsync<DbException>(() => context.Database.ExecuteSqlAsync($"""DELETE FROM "Commits" WHERE "Id" = {id}""", Ct));
  }

  private static async Task<long> AddCommitAsync(
    BranchSeed seed,
    long organizationId,
    long repositoryId,
    int number,
    CommitState state,
    DateTimeOffset? lastUpload,
    bool notified = false,
    UploadState? uploadInProgress = null)
  {
    await using var context = seed.Organizations.ContextOf(organizationId);

    var sha = GitSha.Parse(number.ToString("x40", System.Globalization.CultureInfo.InvariantCulture));
    var commit = await new CommitRepository(context).GetOrCreateAsync(repositoryId, sha, Now.AddDays(-30), Ct);

    if (lastUpload is { } at)
    {
      commit.MarkPending("main", null, at);
    }

    if (state is CommitState.Complete or CommitState.Error)
    {
      commit.RecordFinalization(state, 1, default, 1, 1, CarryforwardStatus.None, "default", lastUpload ?? Now);
    }

    if (notified)
    {
      commit.MarkNotified(Now);
    }

    if (uploadInProgress is { } progress)
    {
      var upload = Upload.Receive(commit.Id, 1, $"key-{number}", [], null, null, null, null, null, null, null, null, PrincipalKind.GitHubOidc, Now);

      if (progress is UploadState.Processing)
      {
        context.Uploads.Add(upload);
        context.Entry(upload).Property(static u => u.State).CurrentValue = UploadState.Processing;
      }
      else
      {
        context.Uploads.Add(upload);
      }
    }

    await context.SaveChangesAsync(Ct);

    return commit.Id;
  }

  private static async Task<long> GetOrCreateAsync(BranchSeed seed, long organizationId, long repositoryId)
  {
    await using var context = seed.Organizations.ContextOf(organizationId);

    return (await new CommitRepository(context).GetOrCreateAsync(repositoryId, Head, Now, Ct)).Id;
  }

  // Each worker opens its connection first, then all go at once: the opening does not spread them out.
  private static async Task<T[]> RaceAsync<T>(
    TwoOrganizationDatabase organizations,
    long organizationId,
    Func<CodeCoverageDbContext, Task<T>> work)
  {
    var go = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var arrived = 0;

    var workers = Enumerable.Range(0, Workers)
                            .Select(
                              _ => Task.Run(
                                async () =>
                                {
                                  await using var context = organizations.ContextOf(organizationId);
                                  await context.Database.OpenConnectionAsync(Ct);

                                  if (Interlocked.Increment(ref arrived) == Workers)
                                  {
                                    go.TrySetResult();
                                  }

                                  await go.Task.WaitAsync(Ct);

                                  return await work(context);
                                },
                                Ct
                              )
                            )
                            .ToArray();

    return await Task.WhenAll(workers).WaitAsync(TimeSpan.FromMinutes(5), Ct);
  }

  #endregion
}
