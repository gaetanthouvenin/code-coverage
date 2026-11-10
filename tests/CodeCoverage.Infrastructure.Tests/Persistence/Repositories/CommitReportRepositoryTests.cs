// ------------------------------------------------------------------------------------------------
// <copyright file="CommitReportRepositoryTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

using CodeCoverage.Application.Exceptions;
using CodeCoverage.Application.Features.Commits;
using CodeCoverage.Domain.Commits;
using CodeCoverage.Domain.Contracts.Commits;
using CodeCoverage.Domain.Contracts.Jobs;
using CodeCoverage.Domain.Coverage;
using CodeCoverage.Domain.Git;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Infrastructure.Diagnostics;
using CodeCoverage.Infrastructure.Identity;
using CodeCoverage.Infrastructure.Jobs;
using CodeCoverage.Infrastructure.Persistence;
using CodeCoverage.Infrastructure.Persistence.Repositories;
using CodeCoverage.Infrastructure.Tests.Support;

using static CodeCoverage.Infrastructure.Tests.Support.RepositorySeeder;

namespace CodeCoverage.Infrastructure.Tests.Persistence.Repositories;

// Every method against two organizations, on both engines: the rows of the other one are neither read nor written.
[Collection(DatabaseCollection.Name)]
public sealed class CommitReportRepositoryTests(DatabaseFixture database) : IDisposable
{
  #region Fields

  private static readonly byte[] Blob = [1, 2, 3];

  private static readonly byte[] Index = [4, 5];

  private static readonly GitSha Origin = GitSha.Parse("c94a8fe5ccb19ba61c4c0873d391e987982fbbd3");

  private static readonly CoverageTotals Totals = new(6, 3, 1, 2, 1);

  private readonly TestMeterFactory _meterFactory = new();

  private readonly JobWakeUpSignal _wakeUpSignal = new(TimeProvider.System);

  #endregion

  #region Properties

  private static CancellationToken Ct => TestContext.Current.CancellationToken;

  #endregion

  #region Methods

  // A job that cannot be queued takes the blob back with it; the same save then writes both.
  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task SaveProcessedUpload_ShouldEnqueueInSameTransaction(DatabaseProvider provider)
  {
    var seed = await SeededAsync(provider);
    var first = seed.Organizations.FirstId;
    var uploadId = await AddUploadAsync(seed);
    await using var context = seed.Organizations.ContextOf(first);
    var upload = await context.Uploads.SingleAsync(u => u.Id == uploadId, Ct);
    var repository = Repository(context, first);

    await Should.ThrowAsync<InvalidOperationException>(
      () => repository.SaveProcessedUploadAsync(upload, Blob, JobRequest.ForOrganization(seed.Organizations.SecondId, "FinalizeCommit", "{}"), Ct)
    );

    (await ReadCoverageAsync(seed, uploadId)).ShouldBeNull();
    (await JobCountAsync(seed)).ShouldBe(0);

    await repository.SaveProcessedUploadAsync(upload, Blob, JobRequest.Create("FinalizeCommit", "{}", dedupKey: "finalize:1"), Ct);

    (await ReadCoverageAsync(seed, uploadId)).ShouldBe(Blob);
    await using var check = seed.Organizations.ContextWithoutOrganization();
    var job = await check.Jobs.AsNoTracking().SingleAsync(Ct);
    job.OrganizationId.ShouldBe(first);
    job.DedupKey.ShouldBe($"o{first}:finalize:1");
    (await _wakeUpSignal.WaitAsync(TimeSpan.Zero, Ct)).ShouldBeTrue();
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task TryAddReceivedUpload_ShouldEnqueueInSameTransaction(DatabaseProvider provider)
  {
    var seed = await SeededAsync(provider);
    var first = seed.Organizations.FirstId;
    await using var context = seed.Organizations.ContextOf(first);
    var repository = Repository(context, first);
    var upload = Received(seed.FirstCommit, "key");

    await Should.ThrowAsync<InvalidOperationException>(
      () => repository.TryAddReceivedUploadAsync(upload, JobRequest.ForOrganization(seed.Organizations.SecondId, "ProcessUpload", "{}"), Ct)
    );

    await using (var empty = seed.Organizations.ContextOf(first))
    {
      (await empty.Uploads.CountAsync(Ct)).ShouldBe(0);
    }

    (await JobCountAsync(seed)).ShouldBe(0);

    (await repository.TryAddReceivedUploadAsync(Received(seed.FirstCommit, "key"), JobRequest.Create("ProcessUpload", "{}"), Ct)).ShouldBeTrue();

    (await JobCountAsync(seed)).ShouldBe(1);
    (await _wakeUpSignal.WaitAsync(TimeSpan.Zero, Ct)).ShouldBeTrue();
  }

  // Each call inserts first, whatever the other does: the second to commit always meets the unique index.
  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task TryAddReceivedUpload_ConcurrentReplays_ShouldAddOneAndQueueOneJob(DatabaseProvider provider)
  {
    var seed = await SeededAsync(provider);
    var first = seed.Organizations.FirstId;

    var added = await Task.WhenAll(
                  Enumerable.Range(0, 2).Select(async _ =>
                    {
                      await using var context = seed.Organizations.ContextOf(first);

                      return await Repository(context, first).TryAddReceivedUploadAsync(Received(seed.FirstCommit, "same"), JobRequest.Create("ProcessUpload", "{}"), Ct);
                    })
                );

    added.Count(static result => result).ShouldBe(1);
    (await JobCountAsync(seed)).ShouldBe(1);
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task TryAddReceivedUpload_CommitOfOtherOrganization_ShouldThrowConflict(DatabaseProvider provider)
  {
    var seed = await SeededAsync(provider);
    var second = seed.Organizations.SecondId;
    await using var context = seed.Organizations.ContextOf(second);

    await Should.ThrowAsync<ConflictException>(
      () => Repository(context, second).TryAddReceivedUploadAsync(Received(seed.FirstCommit, "key"), JobRequest.Create("ProcessUpload", "{}"), Ct)
    );
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task SaveProcessedUpload_Again_ShouldReplaceTheBlob(DatabaseProvider provider)
  {
    var seed = await SeededAsync(provider);
    var uploadId = await AddUploadAsync(seed);
    await SaveProcessedAsync(seed, uploadId, Blob);

    await SaveProcessedAsync(seed, uploadId, [9]);

    (await ReadCoverageAsync(seed, uploadId)).ShouldBe([(byte)9]);
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task SaveProcessedUpload_WithoutBlob_ShouldRemoveThePreviousOne(DatabaseProvider provider)
  {
    var seed = await SeededAsync(provider);
    var uploadId = await AddUploadAsync(seed);
    await SaveProcessedAsync(seed, uploadId, Blob);

    await SaveProcessedAsync(seed, uploadId, null);

    (await ReadCoverageAsync(seed, uploadId)).ShouldBeNull();
    (await JobCountAsync(seed)).ShouldBe(2);
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task SaveProcessedUpload_UploadNotReadInThisScope_ShouldThrow(DatabaseProvider provider)
  {
    var seed = await SeededAsync(provider);
    var first = seed.Organizations.FirstId;
    var uploadId = await AddUploadAsync(seed);
    Upload upload;

    await using (var reader = seed.Organizations.ContextOf(first))
    {
      upload = await reader.Uploads.AsNoTracking().SingleAsync(u => u.Id == uploadId, Ct);
    }

    await using var context = seed.Organizations.ContextOf(first);

    await Should.ThrowAsync<InvalidOperationException>(() => Repository(context, first).SaveProcessedUploadAsync(upload, Blob, Job(), Ct));
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task GetUploadCoverage_OtherOrganization_ShouldReturnNull(DatabaseProvider provider)
  {
    var seed = await SeededAsync(provider);
    var uploadId = await AddUploadAsync(seed);
    await SaveProcessedAsync(seed, uploadId, Blob);

    await using var other = seed.Organizations.ContextOf(seed.Organizations.SecondId);

    (await Repository(other, seed.Organizations.SecondId).GetUploadCoverageAsync(uploadId, Ct)).ShouldBeNull();
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task SaveFinalized_ShouldStoreTheReportSessionsAndTotals(DatabaseProvider provider)
  {
    var seed = await SeededAsync(provider);
    var first = seed.Organizations.FirstId;

    await using (var context = seed.Organizations.ContextOf(first))
    {
      var commit = await context.Commits.SingleAsync(c => c.Id == seed.FirstCommit, Ct);
      Finalize(commit);

      await Repository(context, first).SaveFinalizedAsync(
        commit,
        Blob,
        Index,
        [
          Session.FromUpload(commit.Id, 1, 11, ["unit"], null, "github-actions", "https://example.test/1", Totals),
          Session.CarriedForward(commit.Id, 2, Origin, 7, null, [8], ["e2e"], "CF[1] - e2e", null, null, Totals),
        ],
        [CommitFlagTotals.Create(commit, CommitFlagTotals.ProjectFlag, Totals, false, null), CommitFlagTotals.Create(commit, "e2e", Totals, true, Origin)],
        FinalizationWakeUps.None,
        Ct
      );
    }

    await using var reader = seed.Organizations.ContextOf(first);
    var repository = Repository(reader, first);
    var saved = await reader.Commits.AsNoTracking().SingleAsync(c => c.Id == seed.FirstCommit, Ct);
    var sessions = await reader.CommitSessions.AsNoTracking().OrderBy(static s => s.Number).ToListAsync(Ct);
    var totals = await reader.CommitFlagTotals.AsNoTracking().OrderBy(static t => t.Flag).ToListAsync(Ct);

    (await repository.GetReportAsync(seed.FirstCommit, Ct)).ShouldBe(Blob);
    (await repository.GetFileIndexAsync(seed.FirstCommit, Ct)).ShouldBe(Index);
    saved.State.ShouldBe(CommitState.Complete);
    saved.ReportRevision.ShouldBe(1);
    saved.Version.ShouldBe(1);
    saved.Lines.ShouldBe(10);
    sessions.Count.ShouldBe(2);
    sessions[0].Kind.ShouldBe(SessionKind.Uploaded);
    sessions[0].Name.ShouldBeNull();
    sessions[0].UploadId.ShouldBe(11);
    sessions[0].Flags.ShouldBe(["unit"]);
    sessions[1].Kind.ShouldBe(SessionKind.CarriedForward);
    sessions[1].CarriedForwardFromSha.ShouldBe(Origin);
    sessions[1].Data.ShouldBe([(byte)8]);
    totals.Count.ShouldBe(2);
    totals[0].Flag.ShouldBeEmpty();
    totals[1].Flag.ShouldBe("e2e");
    totals[1].IsCarriedForward.ShouldBeTrue();
    totals[1].CarriedForwardFromSha.ShouldBe(Origin);
    totals[1].Coverage.ShouldBe(60m);
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task SaveFinalized_Again_ShouldReplaceTheRowsInANewRevision(DatabaseProvider provider)
  {
    var seed = await SeededAsync(provider);
    await SaveFinalizedAsync(seed, Blob, 1);

    await SaveFinalizedAsync(seed, [7], 2);

    await using var reader = seed.Organizations.ContextOf(seed.Organizations.FirstId);
    var stored = await reader.CommitReports.AsNoTracking().SingleAsync(r => r.CommitId == seed.FirstCommit, Ct);

    stored.Revision.ShouldBe(2);
    stored.Data.ShouldBe([(byte)7]);
    (await reader.CommitSessions.AsNoTracking().Select(static s => s.Number).ToListAsync(Ct)).ShouldBe([2]);
    (await reader.CommitFlagTotals.AsNoTracking().CountAsync(Ct)).ShouldBe(1);
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task SaveFinalized_WithStaleVersion_ShouldThrowConflict(DatabaseProvider provider)
  {
    var seed = await SeededAsync(provider);
    var first = seed.Organizations.FirstId;
    await using var stale = seed.Organizations.ContextOf(first);
    var loser = await stale.Commits.SingleAsync(c => c.Id == seed.FirstCommit, Ct);
    await SaveFinalizedAsync(seed, Blob, 1);
    Finalize(loser);

    var exception = await Should.ThrowAsync<ConflictException>(
      () => Repository(stale, first).SaveFinalizedAsync(loser, [9], [9], [Session.FromUpload(loser.Id, 5, 11, [], null, null, null, Totals)], [], FinalizationWakeUps.None, Ct)
    );

    exception.ErrorCode.ShouldBe(CommitErrors.ConcurrentSave);
    stale.ChangeTracker.Entries().ShouldBeEmpty();

    await using var reader = seed.Organizations.ContextOf(first);
    (await Repository(reader, first).GetReportAsync(seed.FirstCommit, Ct)).ShouldBe(Blob);
    (await reader.CommitSessions.AsNoTracking().Select(static s => s.Number).ToListAsync(Ct)).ShouldBe([1]);
    (await reader.CommitFlagTotals.AsNoTracking().CountAsync(Ct)).ShouldBe(1);
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task SaveFinalized_WithoutFinalization_ShouldThrow(DatabaseProvider provider)
  {
    var seed = await SeededAsync(provider);
    var first = seed.Organizations.FirstId;
    await using var context = seed.Organizations.ContextOf(first);
    var commit = await context.Commits.SingleAsync(c => c.Id == seed.FirstCommit, Ct);

    await Should.ThrowAsync<InvalidOperationException>(() => Repository(context, first).SaveFinalizedAsync(commit, Blob, Index, [], [], FinalizationWakeUps.None, Ct));
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task SaveFinalized_CommitNotReadInThisScope_ShouldThrow(DatabaseProvider provider)
  {
    var seed = await SeededAsync(provider);
    var first = seed.Organizations.FirstId;
    Commit commit;

    await using (var reader = seed.Organizations.ContextOf(first))
    {
      commit = await reader.Commits.AsNoTracking().SingleAsync(c => c.Id == seed.FirstCommit, Ct);
    }

    Finalize(commit);
    await using var context = seed.Organizations.ContextOf(first);

    await Should.ThrowAsync<InvalidOperationException>(() => Repository(context, first).SaveFinalizedAsync(commit, Blob, Index, [], [], FinalizationWakeUps.None, Ct));
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task SaveFinalized_RowOfAnotherCommit_ShouldThrow(DatabaseProvider provider)
  {
    var seed = await SeededAsync(provider);
    var first = seed.Organizations.FirstId;
    await using var context = seed.Organizations.ContextOf(first);
    var commit = await context.Commits.SingleAsync(c => c.Id == seed.FirstCommit, Ct);
    Finalize(commit);
    var repository = Repository(context, first);

    await Should.ThrowAsync<ArgumentException>(
      () => repository.SaveFinalizedAsync(commit, Blob, Index, [Session.FromUpload(commit.Id + 1000, 1, 11, [], null, null, null, Totals)], [], FinalizationWakeUps.None, Ct)
    );
  }

  // The acceptance of the key sizes on both engines: the longest flag on the longest branch fits the primary key and the index.
  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task SaveFinalized_LongestFlagOnLongestBranch_ShouldFit(DatabaseProvider provider)
  {
    var seed = await SeededAsync(provider);
    var first = seed.Organizations.FirstId;
    var flag = new string('é', CommitFlagTotals.FlagMaxLength);

    await using (var context = seed.Organizations.ContextOf(first))
    {
      var commit = await context.Commits.SingleAsync(c => c.Id == seed.FirstCommit, Ct);
      commit.MarkPending(new string('b', Commit.BranchMaxLength), null, Now);
      Finalize(commit);

      await Repository(context, first).SaveFinalizedAsync(commit, Blob, Index, [], [CommitFlagTotals.Create(commit, flag, Totals, false, null)], FinalizationWakeUps.None, Ct);
    }

    await using var reader = seed.Organizations.ContextOf(first);
    (await reader.CommitFlagTotals.AsNoTracking().SingleAsync(Ct)).Flag.ShouldBe(flag);
  }

  // Codecov flags are case-sensitive: Backend and backend are two flags on both engines.
  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task SaveFinalized_FlagsDifferingByCase_ShouldStoreBoth(DatabaseProvider provider)
  {
    var seed = await SeededAsync(provider);
    var first = seed.Organizations.FirstId;

    await using (var context = seed.Organizations.ContextOf(first))
    {
      var commit = await context.Commits.SingleAsync(c => c.Id == seed.FirstCommit, Ct);
      Finalize(commit);

      await Repository(context, first).SaveFinalizedAsync(
        commit,
        Blob,
        Index,
        [],
        [CommitFlagTotals.Create(commit, "Backend", Totals, false, null), CommitFlagTotals.Create(commit, "backend", Totals, false, null)],
        FinalizationWakeUps.None,
        Ct
      );
    }

    await using var reader = seed.Organizations.ContextOf(first);
    var flags = await reader.CommitFlagTotals.AsNoTracking().Select(static t => t.Flag).ToListAsync(Ct);
    flags.Order(StringComparer.Ordinal).ShouldBe(["Backend", "backend"]);
    (await reader.CommitFlagTotals.AsNoTracking().CountAsync(static t => t.Flag == "backend", Ct)).ShouldBe(1);
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task GetReport_OtherOrganization_ShouldReturnNull(DatabaseProvider provider)
  {
    var seed = await SeededAsync(provider);
    await SaveFinalizedAsync(seed, Blob, 1);

    await using var other = seed.Organizations.ContextOf(seed.Organizations.SecondId);
    var repository = Repository(other, seed.Organizations.SecondId);

    (await repository.GetReportAsync(seed.FirstCommit, Ct)).ShouldBeNull();
    (await repository.GetFileIndexAsync(seed.FirstCommit, Ct)).ShouldBeNull();
    (await other.CommitSessions.AsNoTracking().CountAsync(Ct)).ShouldBe(0);
    (await other.CommitFlagTotals.AsNoTracking().CountAsync(Ct)).ShouldBe(0);
  }

  public void Dispose() => _meterFactory.Dispose();

  private static JobRequest Job() => JobRequest.Create("FinalizeCommit", "{}");

  private static void Finalize(Commit commit) =>
    commit.RecordFinalization(CommitState.Complete, 1, Totals, 2, 1, CarryforwardStatus.None, "default", Now);

  private async Task SaveProcessedAsync(CommitSeed seed, long uploadId, byte[]? blob)
  {
    var first = seed.Organizations.FirstId;
    await using var context = seed.Organizations.ContextOf(first);
    var upload = await context.Uploads.SingleAsync(u => u.Id == uploadId, Ct);

    await Repository(context, first).SaveProcessedUploadAsync(upload, blob, Job(), Ct);
  }

  private async Task SaveFinalizedAsync(CommitSeed seed, byte[] report, int sessionNumber)
  {
    var first = seed.Organizations.FirstId;
    await using var context = seed.Organizations.ContextOf(first);
    var commit = await context.Commits.SingleAsync(c => c.Id == seed.FirstCommit, Ct);
    Finalize(commit);

    await Repository(context, first).SaveFinalizedAsync(
      commit,
      report,
      Index,
      [Session.FromUpload(commit.Id, sessionNumber, 11, [], null, null, null, Totals)],
      [CommitFlagTotals.Create(commit, CommitFlagTotals.ProjectFlag, Totals, false, null)],
      FinalizationWakeUps.None,
      Ct
    );
  }

  private async Task<byte[]?> ReadCoverageAsync(CommitSeed seed, long uploadId)
  {
    await using var reader = seed.Organizations.ContextOf(seed.Organizations.FirstId);

    return await Repository(reader, seed.Organizations.FirstId).GetUploadCoverageAsync(uploadId, Ct);
  }

  private static async Task<int> JobCountAsync(CommitSeed seed)
  {
    await using var check = seed.Organizations.ContextWithoutOrganization();

    return await check.Jobs.AsNoTracking().CountAsync(Ct);
  }

  private static async Task<long> AddUploadAsync(CommitSeed seed)
  {
    await using var context = seed.Organizations.ContextOf(seed.Organizations.FirstId);
    var upload = Upload.Receive(seed.FirstCommit, 1, "key", [], null, null, null, null, null, null, null, null, PrincipalKind.OrganizationToken, Now);

    await new UploadRepository(context).AddAsync(upload, Ct);

    return upload.Id;
  }

  // A commit that completes queues, in its transaction, the waiting commits of its repository, its own organization only.
  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task SaveFinalized_Completed_ShouldQueueTheWaitingCommitsInTheTransaction(DatabaseProvider provider)
  {
    var seed = await SeededAsync(provider);
    var first = seed.Organizations.FirstId;
    var waiting = await AddCommitAsync(seed, first, 'e', static c => c.RecordFinalization(CommitState.Complete, 0, default, 0, 0, CarryforwardStatus.AncestorPending, "source", Now));
    await AddCommitAsync(seed, first, 'f', static c => c.RecordFinalization(CommitState.Complete, 0, default, 0, 0, CarryforwardStatus.None, "source", Now));

    await using (var second = seed.Organizations.ContextOf(seed.Organizations.SecondId))
    {
      var foreign = await second.Commits.SingleAsync(c => c.Id == seed.SecondCommit, Ct);
      foreign.RecordFinalization(CommitState.Complete, 0, default, 0, 0, CarryforwardStatus.AncestorPending, "source", Now);
      await second.SaveChangesAsync(Ct);
    }

    await SaveWithWakeUpsAsync(seed, new FinalizationWakeUps(true, []));

    (await QueuedLockKeysAsync(seed)).ShouldBe([$"o{first}:commit:{waiting}"]);
    (await _wakeUpSignal.WaitAsync(TimeSpan.Zero, Ct)).ShouldBeTrue();
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task SaveFinalized_PendingAncestorEnded_ShouldQueueItsOwnFinalization(DatabaseProvider provider)
  {
    var seed = await SeededAsync(provider);
    var first = seed.Organizations.FirstId;
    var ancestor = await AddCommitAsync(seed, first, 'e', static c => c.RecordFinalization(CommitState.Complete, 0, default, 0, 0, CarryforwardStatus.None, "source", Now));

    await SaveWithWakeUpsAsync(seed, new FinalizationWakeUps(false, [ancestor]));

    (await QueuedLockKeysAsync(seed)).ShouldBe([$"o{first}:commit:{seed.FirstCommit}"]);
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task SaveFinalized_PendingAncestorStillPending_ShouldQueueNothingAndLeaveTheAncestor(DatabaseProvider provider)
  {
    var seed = await SeededAsync(provider);
    var first = seed.Organizations.FirstId;
    var ancestor = await AddCommitAsync(seed, first, 'e', static c => c.MarkPending(null, null, Now));

    await SaveWithWakeUpsAsync(seed, new FinalizationWakeUps(false, [ancestor]));

    (await QueuedLockKeysAsync(seed)).ShouldBeEmpty();
    await using var reader = seed.Organizations.ContextOf(first);
    var stored = await reader.Commits.AsNoTracking().SingleAsync(c => c.Id == ancestor, Ct);
    stored.State.ShouldBe(CommitState.Pending);
    stored.Version.ShouldBe(0);
  }

  // The ancestor is being finalized and holds its row: the waiting commit waits for it, then sees it ended.
  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task SaveFinalized_AncestorSavedAtTheSameTime_ShouldWaitForItAndQueueItself(DatabaseProvider provider)
  {
    var seed = await SeededAsync(provider);
    var first = seed.Organizations.FirstId;
    var ancestor = await AddCommitAsync(seed, first, 'e', static c => c.MarkPending(null, null, Now));

    await using var holder = seed.Organizations.ContextOf(first);
    await using var transaction = await holder.Database.BeginTransactionAsync(Ct);
    var held = await holder.Commits.SingleAsync(c => c.Id == ancestor, Ct);
    held.RecordFinalization(CommitState.Complete, 0, default, 0, 0, CarryforwardStatus.None, "source", Now);
    await holder.SaveChangesAsync(Ct);

    var save = SaveWithWakeUpsAsync(seed, new FinalizationWakeUps(false, [ancestor]));
    (await Task.WhenAny(save, Task.Delay(TimeSpan.FromSeconds(1), Ct))).ShouldNotBe(save, "the waiting commit must wait for the row of the ancestor");

    await transaction.CommitAsync(Ct);
    await save.WaitAsync(TimeSpan.FromMinutes(1), Ct);

    (await QueuedLockKeysAsync(seed)).ShouldBe([$"o{first}:commit:{seed.FirstCommit}"]);
  }

  // The waiting commit holds the row of its ancestor: the ancestor that completes waits for it, then lists it.
  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task SaveFinalized_WaitingCommitSavedAtTheSameTime_ShouldWaitForItAndQueueIt(DatabaseProvider provider)
  {
    var seed = await SeededAsync(provider);
    var first = seed.Organizations.FirstId;
    var waiting = await AddCommitAsync(seed, first, 'e', static c => c.MarkPending(null, null, Now));

    await using (var pending = seed.Organizations.ContextOf(first))
    {
      var commit = await pending.Commits.SingleAsync(c => c.Id == seed.FirstCommit, Ct);
      commit.MarkPending(null, null, Now);
      await pending.SaveChangesAsync(Ct);
    }

    // What the save of the waiting commit does: it writes its state, then locks the row of its pending ancestor.
    await using var holder = seed.Organizations.ContextOf(first);
    await using var transaction = await holder.Database.BeginTransactionAsync(Ct);
    var held = await holder.Commits.SingleAsync(c => c.Id == waiting, Ct);
    held.RecordFinalization(CommitState.Complete, 0, default, 0, 0, CarryforwardStatus.AncestorPending, "source", Now);
    await holder.SaveChangesAsync(Ct);
    await holder.Commits.Where(c => c.Id == seed.FirstCommit).ExecuteUpdateAsync(static s => s.SetProperty(static c => c.UpdatedAt, static c => c.UpdatedAt), Ct);

    var save = SaveWithWakeUpsAsync(seed, new FinalizationWakeUps(true, []));
    (await Task.WhenAny(save, Task.Delay(TimeSpan.FromSeconds(1), Ct))).ShouldNotBe(save, "the ancestor must wait for the waiting commit");

    await transaction.CommitAsync(Ct);
    await save.WaitAsync(TimeSpan.FromMinutes(1), Ct);

    (await QueuedLockKeysAsync(seed)).ShouldBe([$"o{first}:commit:{waiting}"]);
  }

  // Chain P <- A <- D, P and A pending: A finalizes while D does. A holds its row and is about to lock P; D, whose
  // pending ancestors are A then P, must lock them in that order, or it holds P while waiting for A and the two
  // transactions deadlock.
  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task SaveFinalized_TwoCommitsSharingAFartherAncestor_ShouldNotDeadlock(DatabaseProvider provider)
  {
    var seed = await SeededAsync(provider);
    var first = seed.Organizations.FirstId;
    var farther = await AddCommitAsync(seed, first, '1', static c => c.MarkPending(null, null, Now));
    var closer = await AddCommitAsync(seed, first, '2', static c => c.MarkPending(null, null, Now));
    var head = await AddCommitAsync(seed, first, '3', static c => c.MarkPending(null, null, Now));
    var holding = new HoldingCommandInterceptor(static sql => sql.Replace("\"", string.Empty, StringComparison.Ordinal)
                                                                 .Replace("[", string.Empty, StringComparison.Ordinal)
                                                                 .Replace("]", string.Empty, StringComparison.Ordinal)
                                                                 .Replace(" ", string.Empty, StringComparison.Ordinal)
                                                                 .Contains("UpdatedAt=c.UpdatedAt", StringComparison.Ordinal));

    var closerSave = SaveCommitAsync(seed, provider, closer, new FinalizationWakeUps(false, [farther]), holding);
    await holding.Reached.Task.WaitAsync(TimeSpan.FromMinutes(1), Ct);

    var headSave = SaveCommitAsync(seed, provider, head, new FinalizationWakeUps(false, [closer, farther]), null);
    (await Task.WhenAny(headSave, Task.Delay(TimeSpan.FromSeconds(1), Ct))).ShouldNotBe(headSave, "the head must wait for the row of its closer ancestor");

    holding.Gate.SetResult();
    await Task.WhenAll(closerSave, headSave).WaitAsync(TimeSpan.FromMinutes(1), Ct);

    (await QueuedLockKeysAsync(seed)).ShouldBe([$"o{first}:commit:{head}"]);
  }

  private async Task SaveCommitAsync(CommitSeed seed, DatabaseProvider provider, long commitId, FinalizationWakeUps wakeUps, HoldingCommandInterceptor? interceptor)
  {
    var first = seed.Organizations.FirstId;
    var options = new DbContextOptionsBuilder<CodeCoverageDbContext>();
    options.UseCodeCoverageDatabase(provider, seed.Organizations.ConnectionString);

    if (interceptor is not null)
    {
      options.AddInterceptors(interceptor);
    }

    var organization = new OrganizationContext();
    organization.Enter(first);

    await using var context = new CodeCoverageDbContext(options.Options, organization);
    var commit = await context.Commits.SingleAsync(c => c.Id == commitId, Ct);
    Finalize(commit);

    await Repository(context, first).SaveFinalizedAsync(commit, Blob, Index, [], [], wakeUps, Ct);
  }

  private static async Task<long> AddCommitAsync(CommitSeed seed, long organizationId, char sha, Action<Commit> state)
  {
    await using var context = seed.Organizations.ContextOf(organizationId);
    var commit = await new CommitRepository(context).GetOrCreateAsync(seed.FirstRepository, GitSha.Parse(new string(sha, GitSha.Length)), Now, Ct);

    state(commit);
    await context.SaveChangesAsync(Ct);

    return commit.Id;
  }

  private static async Task<List<string>> QueuedLockKeysAsync(CommitSeed seed)
  {
    await using var check = seed.Organizations.ContextWithoutOrganization();

    return await check.Jobs.AsNoTracking().Where(static j => j.Type == JobTypes.FinalizeCommit).Select(static j => j.LockKey!).ToListAsync(Ct);
  }

  private async Task SaveWithWakeUpsAsync(CommitSeed seed, FinalizationWakeUps wakeUps)
  {
    var first = seed.Organizations.FirstId;
    await using var context = seed.Organizations.ContextOf(first);
    var commit = await context.Commits.SingleAsync(c => c.Id == seed.FirstCommit, Ct);
    Finalize(commit);

    await Repository(context, first).SaveFinalizedAsync(commit, Blob, Index, [], [], wakeUps, Ct);
  }

  private static Upload Received(long commitId, string key) =>
    Upload.Receive(commitId, 1, key, [], null, null, null, null, null, null, null, null, PrincipalKind.OrganizationToken, Now);

  private CommitReportRepository Repository(CodeCoverageDbContext context, long organizationId)
  {
    var organization = new OrganizationContext();
    organization.Enter(organizationId);

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

    return new CommitReportRepository(context, queue, _wakeUpSignal);
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
