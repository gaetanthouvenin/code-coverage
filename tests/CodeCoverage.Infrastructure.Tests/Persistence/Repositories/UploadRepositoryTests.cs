// ------------------------------------------------------------------------------------------------
// <copyright file="UploadRepositoryTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.EntityFrameworkCore;

using CodeCoverage.Application.Exceptions;
using CodeCoverage.Application.Exceptions.ErrorCodes;
using CodeCoverage.Domain.Commits;
using CodeCoverage.Infrastructure.Persistence;
using CodeCoverage.Infrastructure.Persistence.Repositories;
using CodeCoverage.Infrastructure.Tests.Support;

using static CodeCoverage.Infrastructure.Tests.Support.RepositorySeeder;

namespace CodeCoverage.Infrastructure.Tests.Persistence.Repositories;

// Every method against two organizations, on both engines: the rows of the other one are neither read nor written.
[Collection(DatabaseCollection.Name)]
public sealed class UploadRepositoryTests(DatabaseFixture database)
{
  #region Fields

  private const string Key = "idempotency-key";

  #endregion

  #region Properties

  private static CancellationToken Ct => TestContext.Current.CancellationToken;

  #endregion

  #region Methods

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task Add_ShouldStoreTheUpload(DatabaseProvider provider)
  {
    var seed = await SeededAsync(provider);
    var upload = Upload.Receive(
      seed.FirstCommit,
      1,
      Key,
      ["unit", "linux"],
      "linux",
      "github-actions",
      "build",
      "job",
      "https://example.test/build",
      "/work",
      "1.0.0",
      5,
      PrincipalKind.OrganizationToken,
      Now
    );

    await using (var writer = seed.Organizations.ContextOf(seed.Organizations.FirstId))
    {
      await new UploadRepository(writer).AddAsync(upload, Ct);
    }

    await using var reader = seed.Organizations.ContextOf(seed.Organizations.FirstId);
    var saved = (await new UploadRepository(reader).FindByPublicIdAsync(upload.PublicId, Ct)).ShouldNotBeNull();

    saved.Id.ShouldBePositive();
    saved.CommitId.ShouldBe(seed.FirstCommit);
    saved.State.ShouldBe(UploadState.Received);
    saved.Flags.ShouldBe(["unit", "linux"]);
    saved.Name.ShouldBe("linux");
    saved.PrincipalKind.ShouldBe(PrincipalKind.OrganizationToken);
    saved.TokenId.ShouldBe(5);
    saved.ProcessingLog.MessageTotal.ShouldBe(0);
    saved.ReceivedAt.ShouldBe(Now);
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task Add_SameKeyOnTheCommit_ShouldThrowConflictAndLeaveTheScopeUsable(DatabaseProvider provider)
  {
    var seed = await SeededAsync(provider);
    await AddAsync(seed.Organizations, seed.Organizations.FirstId, seed.FirstCommit, 1, Key);

    await using var context = seed.Organizations.ContextOf(seed.Organizations.FirstId);
    var uploads = new UploadRepository(context);

    var exception = await Should.ThrowAsync<ConflictException>(() => uploads.AddAsync(New(seed.FirstCommit, 2, Key), Ct));

    exception.ErrorCode.ShouldBe(GeneralErrors.ConcurrentChange);
    (await uploads.FindByIdempotencyKeyAsync(seed.FirstCommit, Key, Ct)).ShouldNotBeNull().Sequence.ShouldBe(1);
    await uploads.AddAsync(New(seed.FirstCommit, 3, "other-key"), Ct);
  }

  // PostgreSQL aborts a transaction on a failed statement: the replay must not cost the caller its transaction.
  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task Add_SameKeyInATransaction_ShouldLeaveTheTransactionUsable(DatabaseProvider provider)
  {
    var seed = await SeededAsync(provider);
    await AddAsync(seed.Organizations, seed.Organizations.FirstId, seed.FirstCommit, 1, Key);

    await using var context = seed.Organizations.ContextOf(seed.Organizations.FirstId);
    var uploads = new UploadRepository(context);
    await using var transaction = await context.Database.BeginTransactionAsync(Ct);

    await Should.ThrowAsync<ConflictException>(() => uploads.AddAsync(New(seed.FirstCommit, 2, Key), Ct));

    (await uploads.ListByCommitAsync(seed.FirstCommit, Ct)).ShouldHaveSingleItem();
    await transaction.CommitAsync(Ct);
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task Add_SameKeyOnTwoCommits_ShouldKeepBoth(DatabaseProvider provider)
  {
    var seed = await SeededAsync(provider);
    await using var context = seed.Organizations.ContextOf(seed.Organizations.FirstId);
    var other = await new CommitRepository(context).GetOrCreateAsync(seed.FirstRepository, NewHead, Now, Ct);
    var uploads = new UploadRepository(context);

    await uploads.AddAsync(New(seed.FirstCommit, 1, Key), Ct);
    await uploads.AddAsync(New(other.Id, 1, Key), Ct);

    (await uploads.ListByCommitAsync(other.Id, Ct)).ShouldHaveSingleItem();
  }

  // The unique index does not hold the organization: without the check, the key would be taken in the other one.
  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task Add_CommitOfOtherOrganization_ShouldThrowConflictAndWriteNothing(DatabaseProvider provider)
  {
    var seed = await SeededAsync(provider);

    await using (var first = seed.Organizations.ContextOf(seed.Organizations.FirstId))
    {
      var exception = await Should.ThrowAsync<ConflictException>(() => new UploadRepository(first).AddAsync(New(seed.SecondCommit, 1, Key), Ct));

      exception.ErrorCode.ShouldBe(GeneralErrors.ConcurrentChange);
    }

    await using var second = seed.Organizations.ContextOf(seed.Organizations.SecondId);
    var uploads = new UploadRepository(second);
    (await uploads.ListByCommitAsync(seed.SecondCommit, Ct)).ShouldBeEmpty();
    await uploads.AddAsync(New(seed.SecondCommit, 1, Key), Ct);
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task FindByPublicId_OtherOrganization_ShouldReturnNull(DatabaseProvider provider)
  {
    var seed = await SeededAsync(provider);
    var foreign = await AddAsync(seed.Organizations, seed.Organizations.SecondId, seed.SecondCommit, 1, Key);

    await using var first = seed.Organizations.ContextOf(seed.Organizations.FirstId);

    (await new UploadRepository(first).FindByPublicIdAsync(foreign.PublicId, Ct)).ShouldBeNull();
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task FindByIdempotencyKey_OtherOrganization_ShouldReturnNull(DatabaseProvider provider)
  {
    var seed = await SeededAsync(provider);
    await AddAsync(seed.Organizations, seed.Organizations.SecondId, seed.SecondCommit, 1, Key);

    await using var first = seed.Organizations.ContextOf(seed.Organizations.FirstId);

    (await new UploadRepository(first).FindByIdempotencyKeyAsync(seed.SecondCommit, Key, Ct)).ShouldBeNull();
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task ListByCommit_ShouldSortBySequence(DatabaseProvider provider)
  {
    var seed = await SeededAsync(provider);
    await using var context = seed.Organizations.ContextOf(seed.Organizations.FirstId);
    var uploads = new UploadRepository(context);
    await uploads.AddAsync(New(seed.FirstCommit, 2, "b"), Ct);
    await uploads.AddAsync(New(seed.FirstCommit, 1, "a"), Ct);

    var listed = await uploads.ListByCommitAsync(seed.FirstCommit, Ct);

    listed.Select(static u => u.Sequence).ShouldBe([1, 2]);
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task ListByCommit_OtherOrganization_ShouldBeEmpty(DatabaseProvider provider)
  {
    var seed = await SeededAsync(provider);
    await AddAsync(seed.Organizations, seed.Organizations.SecondId, seed.SecondCommit, 1, Key);

    await using var first = seed.Organizations.ContextOf(seed.Organizations.FirstId);

    (await new UploadRepository(first).ListByCommitAsync(seed.SecondCommit, Ct)).ShouldBeEmpty();
  }

  // Raw on purpose: the Domain has nothing yet that writes a log.
  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task Find_StoredLog_ShouldReadTheItemsAndTotals(DatabaseProvider provider)
  {
    var seed = await SeededAsync(provider);
    var upload = await AddAsync(seed.Organizations, seed.Organizations.FirstId, seed.FirstCommit, 1, Key);
    await using var context = seed.Organizations.ContextOf(seed.Organizations.FirstId);
    const string Json = """{"messages":["unsupported"],"messageTotal":2,"paths":["src/a.cs"],"pathTotal":3}""";
    await context.Database.ExecuteSqlAsync($"""UPDATE "Uploads" SET "ProcessingLog" = {Json}""", Ct);

    var read = (await new UploadRepository(context).FindByPublicIdAsync(upload.PublicId, Ct)).ShouldNotBeNull();

    read.ProcessingLog.Messages.ShouldBe(["unsupported"]);
    read.ProcessingLog.MessageTotal.ShouldBe(2);
    read.ProcessingLog.Paths.ShouldBe(["src/a.cs"]);
    read.ProcessingLog.PathTotal.ShouldBe(3);
  }

  private static Upload New(long commitId, int sequence, string key) =>
    Upload.Receive(commitId, sequence, key, [], null, null, null, null, null, null, null, null, PrincipalKind.GitHubOidc, Now);

  private static async Task<Upload> AddAsync(TwoOrganizationDatabase organizations, long organizationId, long commitId, int sequence, string key)
  {
    await using var context = organizations.ContextOf(organizationId);
    var upload = New(commitId, sequence, key);

    await new UploadRepository(context).AddAsync(upload, Ct);

    return upload;
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
