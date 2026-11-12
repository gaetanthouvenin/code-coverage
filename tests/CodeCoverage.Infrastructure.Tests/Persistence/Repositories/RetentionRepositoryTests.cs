// ------------------------------------------------------------------------------------------------
// <copyright file="RetentionRepositoryTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.EntityFrameworkCore;

using CodeCoverage.Domain.Commits;
using CodeCoverage.Domain.Contracts.Forges;
using CodeCoverage.Domain.Contracts.Uploads;
using CodeCoverage.Domain.Coverage;
using CodeCoverage.Domain.Diffs;
using CodeCoverage.Domain.Git;
using CodeCoverage.Domain.Operations;
using CodeCoverage.Domain.PullRequests;
using CodeCoverage.Domain.Repositories;
using CodeCoverage.Infrastructure.Commits;
using CodeCoverage.Infrastructure.GitHub;
using CodeCoverage.Infrastructure.Jobs;
using CodeCoverage.Infrastructure.Persistence;
using CodeCoverage.Infrastructure.Persistence.Repositories;
using CodeCoverage.Infrastructure.Tests.Support;
using CodeCoverage.Infrastructure.Uploads;

using static CodeCoverage.Infrastructure.Tests.Support.RepositorySeeder;

namespace CodeCoverage.Infrastructure.Tests.Persistence.Repositories;

// The purge on both engines, against two organizations: the rows of the other one are never touched.
[Collection(DatabaseCollection.Name)]
public sealed class RetentionRepositoryTests(DatabaseFixture database)
{
  #region Fields

  private static readonly DateTimeOffset Old = Now.AddDays(-200);

  private static readonly DateTimeOffset Before = Now.AddDays(-180);

  private static readonly CoverageTotals Totals = new(6, 3, 1, 0, 0);

  #endregion

  #region Properties

  private static CancellationToken Ct => TestContext.Current.CancellationToken;

  #endregion

  #region Methods

  // Off the default branch and old: compacted. The default branch, no branch, a recent commit, the head of a branch and
  // every SHA of an open pull request are kept; a closed pull request keeps nothing.
  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task Purge_ShouldKeepDefaultBranchAndOpenPrReports(DatabaseProvider provider)
  {
    var seed = await AddRepositoriesAsync(database, provider, Ct);
    var first = seed.Organizations.FirstId;
    var repository = seed.FirstRepository;

    var main = await AddCommitAsync(seed, first, repository, Sha('1'), "main", Old);
    var compacted = await AddCommitAsync(seed, first, repository, Sha('2'), "feature", Old);
    var openHead = await AddCommitAsync(seed, first, repository, Sha('3'), "feature", Old);
    var openBase = await AddCommitAsync(seed, first, repository, Sha('4'), "feature", Old);
    var recent = await AddCommitAsync(seed, first, repository, Sha('5'), "feature", Now.AddDays(-10));
    var noBranch = await AddCommitAsync(seed, first, repository, Sha('6'), null, Old);
    var branchHead = await AddCommitAsync(seed, first, repository, Sha('7'), "feature", Old);
    var closedHead = await AddCommitAsync(seed, first, repository, Sha('8'), "feature", Old);

    await using (var context = seed.Organizations.ContextOf(first))
    {
      context.Branches.Add(Branch.Create(repository, "feature", Sha('7'), Now));
      context.PullRequests.Add(PullRequest.Create(repository, new ForgePullInfo(1, ForgePullState.Open, "open", null, Sha('3'), "feature", "main", Sha('4')), Now));
      context.PullRequests.Add(PullRequest.Create(repository, new ForgePullInfo(2, ForgePullState.Closed, "closed", null, Sha('8'), "feature", "main", Sha('1')), Now));
      await context.SaveChangesAsync(Ct);
    }

    await using (var context = seed.Organizations.ContextOf(first))
    {
      (await new RetentionRepository(context).CompactCommitsAsync(Before, 5000, Ct)).ShouldBe(2);
    }

    await using var check = seed.Organizations.ContextOf(first);
    var states = await check.Commits.AsNoTracking().ToDictionaryAsync(static c => c.Id, static c => c.State, Ct);

    states[compacted.Commit].ShouldBe(CommitState.Compacted);
    states[closedHead.Commit].ShouldBe(CommitState.Compacted);

    foreach (var kept in new[] { main, openHead, openBase, recent, noBranch, branchHead })
    {
      states[kept.Commit].ShouldBe(CommitState.Complete);
    }

    var reports = await check.CommitReports.AsNoTracking().Select(static r => r.CommitId).ToListAsync(Ct);
    var coverages = await check.UploadCoverages.AsNoTracking().Select(static r => r.UploadId).ToListAsync(Ct);

    reports.ShouldNotContain(compacted.Commit);
    coverages.ShouldNotContain(compacted.Upload);
    reports.ShouldContain(openHead.Commit);
    coverages.ShouldContain(main.Upload);
    reports.Count.ShouldBe(6);

    // The commit and its sessions stay: history and badges.
    (await check.CommitSessions.AsNoTracking().CountAsync(s => s.CommitId == compacted.Commit, Ct)).ShouldBe(1);
  }

  // A commit whose session or upload another commit carries forward keeps its reports; an upload still processing
  // keeps its commit too.
  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task Purge_ShouldKeepCarryforwardSources(DatabaseProvider provider)
  {
    var seed = await AddRepositoriesAsync(database, provider, Ct);
    var first = seed.Organizations.FirstId;
    var repository = seed.FirstRepository;
    var source = await AddCommitAsync(seed, first, repository, Sha('1'), "feature", Old);
    var carrier = await AddCommitAsync(seed, first, repository, Sha('2'), "main", Old);
    var processing = await AddCommitAsync(seed, first, repository, Sha('3'), "feature", Old);

    await using (var context = seed.Organizations.ContextOf(first))
    {
      context.CommitSessions.Add(Session.CarriedForward(carrier.Commit, 2, Sha('1'), source.Session, source.Upload, null, [], null, null, null, Totals));
      await context.SaveChangesAsync(Ct);
      await context.Uploads.Where(u => u.Id == processing.Upload).ExecuteUpdateAsync(s => s.SetProperty(u => u.State, UploadState.Processing), Ct);
    }

    await using (var context = seed.Organizations.ContextOf(first))
    {
      (await new RetentionRepository(context).CompactCommitsAsync(Before, 5000, Ct)).ShouldBe(0);
    }

    await using var check = seed.Organizations.ContextOf(first);
    (await check.Commits.AsNoTracking().CountAsync(static c => c.State == CommitState.Compacted, Ct)).ShouldBe(0);
    (await check.UploadCoverages.AsNoTracking().CountAsync(u => u.UploadId == source.Upload, Ct)).ShouldBe(1);
  }

  // Batches smaller than the rows: every old row goes, a queued or running job never does, a recent one stays.
  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task Purge_ShouldDeleteInBatches(DatabaseProvider provider)
  {
    var seed = await AddRepositoriesAsync(database, provider, Ct);
    var first = seed.Organizations.FirstId;

    await using (var context = seed.Organizations.ContextWithoutOrganization())
    {
      for (var i = 0; i < 5; i++)
      {
        context.Jobs.Add(Job(JobState.Succeeded, Old));
        context.Jobs.Add(Job(i % 2 == 0 ? JobState.Failed : JobState.Dead, Old));
        context.WebhookDeliveries.Add(Delivery($"old-{i}", Old));
      }

      context.Jobs.Add(Job(JobState.Queued, null));
      context.Jobs.Add(Job(JobState.Running, null));
      context.Jobs.Add(Job(JobState.Succeeded, Now));
      context.WebhookDeliveries.Add(Delivery("recent", Now));
      await context.SaveChangesAsync(Ct);
    }

    await using (var context = seed.Organizations.ContextWithoutOrganization())
    {
      var retention = new RetentionRepository(context);

      (await retention.PurgeJobsAsync(Before, 3, Ct)).ShouldBe(10);
      (await retention.PurgeWebhookDeliveriesAsync(Before, 2, Ct)).ShouldBe(5);
    }

    await using (var context = seed.Organizations.ContextOf(first))
    {
      var raw = await AddRawFileAsync(context, Now.AddDays(-1));
      await AddRawFileAsync(context, Now.AddDays(-2));
      await AddRawFileAsync(context, Now.AddDays(-3));
      var live = await AddRawFileAsync(context, Now.AddDays(1));

      (await new RetentionRepository(context).PurgeRawUploadsAsync(Now, 2, Ct)).ShouldBe(3);

      (await context.UploadRawFiles.AsNoTracking().Select(static f => f.Id).ToListAsync(Ct)).ShouldBe([live]);
      (await context.UploadRawChunks.AsNoTracking().CountAsync(c => c.RawFileId == raw, Ct)).ShouldBe(0);
      (await context.UploadRawChunks.AsNoTracking().CountAsync(Ct)).ShouldBe(1);
    }

    await using var check = seed.Organizations.ContextWithoutOrganization();
    (await check.Jobs.AsNoTracking().Select(static j => j.State).ToListAsync(Ct)).ShouldBe([JobState.Queued, JobState.Running, JobState.Succeeded], ignoreOrder: true);
    (await check.WebhookDeliveries.AsNoTracking().Select(static d => d.DeliveryId).ToListAsync(Ct)).ShouldBe(["recent"]);
  }

  // The purge of the first organization leaves the commits, raw files and caches of the second one.
  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task Purge_OtherOrganization_ShouldLeaveItsRows(DatabaseProvider provider)
  {
    var seed = await AddRepositoriesAsync(database, provider, Ct);
    var first = seed.Organizations.FirstId;
    var second = seed.Organizations.SecondId;
    var mine = await AddCommitAsync(seed, first, seed.FirstRepository, Sha('1'), "feature", Old);
    var theirs = await AddCommitAsync(seed, second, seed.SecondRepository, Sha('1'), "feature", Old);

    await using (var context = seed.Organizations.ContextOf(second))
    {
      await AddRawFileAsync(context, Now.AddDays(-1));
      AddCaches(context, seed.SecondRepository, Old);
      await context.SaveChangesAsync(Ct);
    }

    await using (var context = seed.Organizations.ContextOf(first))
    {
      AddCaches(context, seed.FirstRepository, Old);
      AddCaches(context, seed.FirstRepository, Now, Sha('9'));
      await context.SaveChangesAsync(Ct);

      var retention = new RetentionRepository(context);

      (await retention.PurgeRawUploadsAsync(Now, 5000, Ct)).ShouldBe(0);
      (await retention.CompactCommitsAsync(Before, 5000, Ct)).ShouldBe(1);
      (await retention.PurgeForgeCachesAsync(Now.AddDays(-30), 5000, Ct)).ShouldBe(2);
    }

    await using (var check = seed.Organizations.ContextOf(first))
    {
      (await check.Commits.AsNoTracking().SingleAsync(c => c.Id == mine.Commit, Ct)).State.ShouldBe(CommitState.Compacted);
      (await check.DiffCache.AsNoTracking().CountAsync(Ct)).ShouldBe(1);
      (await check.RepositoryTrees.AsNoTracking().CountAsync(Ct)).ShouldBe(1);
    }

    await using var other = seed.Organizations.ContextOf(second);
    (await other.Commits.AsNoTracking().SingleAsync(c => c.Id == theirs.Commit, Ct)).State.ShouldBe(CommitState.Complete);
    (await other.CommitReports.AsNoTracking().CountAsync(Ct)).ShouldBe(1);
    (await other.UploadRawFiles.AsNoTracking().CountAsync(Ct)).ShouldBe(1);
    (await other.DiffCache.AsNoTracking().CountAsync(Ct)).ShouldBe(1);
    (await other.RepositoryTrees.AsNoTracking().CountAsync(Ct)).ShouldBe(1);
  }

  private static GitSha Sha(char c) => GitSha.Parse(new string(c, GitSha.Length));

  // A finalized commit with one processed upload, its session, its coverage and its merged report.
  private static async Task<SeededCommit> AddCommitAsync(
    BranchSeed seed,
    long organizationId,
    long repositoryId,
    GitSha sha,
    string? branch,
    DateTimeOffset completedAt)
  {
    await using var context = seed.Organizations.ContextOf(organizationId);
    var commit = await new CommitRepository(context).GetOrCreateAsync(repositoryId, sha, Now, Ct);
    var upload = Upload.Receive(commit.Id, 1, sha.Value, [], null, null, null, null, null, null, null, null, PrincipalKind.OrganizationToken, Now);

    await new UploadRepository(context).AddAsync(upload, Ct);

    var session = Session.FromUpload(commit.Id, 1, upload.Id, [], null, null, null, Totals);
    context.CommitSessions.Add(session);
    context.UploadCoverages.Add(new UploadCoverageRecord { UploadId = upload.Id, Data = [1, 2, 3] });
    context.CommitReports.Add(new CommitReportRecord { CommitId = commit.Id, Revision = 1, Data = [4, 5], FileIndex = [6] });
    await context.SaveChangesAsync(Ct);

    await context.Uploads.Where(u => u.Id == upload.Id).ExecuteUpdateAsync(s => s.SetProperty(u => u.State, UploadState.Processed), Ct);
    await context.Commits
                 .Where(c => c.Id == commit.Id)
                 .ExecuteUpdateAsync(s => s.SetProperty(c => c.State, CommitState.Complete).SetProperty(c => c.Branch, branch).SetProperty(c => c.CompletedAt, completedAt), Ct);

    return new SeededCommit(commit.Id, upload.Id, session.Id);
  }

  private static async Task<long> AddRawFileAsync(CodeCoverageDbContext context, DateTimeOffset expiresAt)
  {
    var file = new UploadRawFileRecord
    {
      UploadPublicId = Guid.NewGuid(),
      Kind = UploadPartKind.Report,
      FileName = "coverage.xml",
      Sha256 = new byte[UploadRawFileRecord.Sha256Length],
      CompressedSize = 1,
      UncompressedSize = 1,
      ExpiresAt = expiresAt,
    };

    context.UploadRawFiles.Add(file);
    await context.SaveChangesAsync(Ct);
    context.UploadRawChunks.Add(new UploadRawChunkRecord { RawFileId = file.Id, ChunkIndex = 0, Data = [1] });
    await context.SaveChangesAsync(Ct);

    return file.Id;
  }

  private static void AddCaches(CodeCoverageDbContext context, long repositoryId, DateTimeOffset fetchedAt, GitSha? head = null)
  {
    context.DiffCache.Add(
      new DiffCacheRecord { RepositoryId = repositoryId, BaseSha = Sha('a'), HeadSha = head ?? Sha('b'), Kind = DiffSource.Compare, Data = [1], FetchedAt = fetchedAt }
    );
    context.RepositoryTrees.Add(new RepositoryTreeRecord { RepositoryId = repositoryId, Sha = head ?? Sha('b'), Data = [1], FetchedAt = fetchedAt });
  }

  private static JobRecord Job(JobState state, DateTimeOffset? completedAt) =>
    new()
    {
      Type = "Sample",
      PayloadJson = "{}",
      State = state,
      MaxAttempts = 8,
      RunAfter = Old,
      FirstQueuedAt = Old,
      CorrelationId = "test",
      CreatedAt = Old,
      CompletedAt = completedAt,
    };

  private static WebhookDeliveryRecord Delivery(string id, DateTimeOffset receivedAt) =>
    new() { DeliveryId = id, Event = "push", Payload = [1], ReceivedAt = receivedAt };

  #endregion
}
