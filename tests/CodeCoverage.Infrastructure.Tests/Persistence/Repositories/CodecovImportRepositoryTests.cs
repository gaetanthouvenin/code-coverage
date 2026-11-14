// ------------------------------------------------------------------------------------------------
// <copyright file="CodecovImportRepositoryTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.EntityFrameworkCore;

using CodeCoverage.Domain.Codecov;
using CodeCoverage.Domain.Contracts.Jobs;
using CodeCoverage.Infrastructure.Jobs;
using CodeCoverage.Infrastructure.Persistence;
using CodeCoverage.Infrastructure.Persistence.Repositories;
using CodeCoverage.Infrastructure.Tests.Support;

using static CodeCoverage.Infrastructure.Tests.Support.RepositorySeeder;

namespace CodeCoverage.Infrastructure.Tests.Persistence.Repositories;

// Every method against two organizations, on both engines: the rows of the other one are neither read nor written.
[Collection(DatabaseCollection.Name)]
public sealed class CodecovImportRepositoryTests(DatabaseFixture database)
{
  #region Properties

  private static CancellationToken Ct => TestContext.Current.CancellationToken;

  #endregion

  #region Methods

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task Start_ShouldSaveTheImportAndQueueItsJobWithoutTheToken(DatabaseProvider provider)
  {
    var seed = await AddRepositoriesAsync(database, provider, Ct);
    var import = CodecovImport.Start(seed.FirstRepository, "protected-token-value", Now);

    await using (var writer = seed.Organizations.ContextOf(seed.Organizations.FirstId))
    {
      await Repository(writer, seed.Organizations.FirstId).StartAsync(import, Ct);
    }

    import.Id.ShouldBePositive();

    await using var reader = seed.Organizations.ContextOf(seed.Organizations.FirstId);
    var read = (await Repository(reader, seed.Organizations.FirstId).FindAsync(import.Id, Ct)).ShouldNotBeNull();
    read.RepositoryId.ShouldBe(seed.FirstRepository);
    read.ProtectedToken.ShouldBe("protected-token-value");
    read.NextPage.ShouldBe(1);

    await using var instance = seed.Organizations.ContextWithoutOrganization();
    var job = await instance.Jobs.SingleAsync(j => j.Type == JobTypes.ImportCodecovHistory, Ct);
    job.OrganizationId.ShouldBe(seed.Organizations.FirstId);
    CodecovImportJobs.ReadImportId(job.PayloadJson).ShouldBe(import.Id);
    job.PayloadJson.ShouldNotContain("protected-token-value");
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task Update_ShouldSaveTheProgress(DatabaseProvider provider)
  {
    var seed = await AddRepositoriesAsync(database, provider, Ct);
    var id = await StartAsync(seed, seed.Organizations.FirstId, seed.FirstRepository, Now);

    await using (var writer = seed.Organizations.ContextOf(seed.Organizations.FirstId))
    {
      var repository = Repository(writer, seed.Organizations.FirstId);
      var import = (await repository.FindAsync(id, Ct)).ShouldNotBeNull();
      import.RecordPage(42);
      await repository.UpdateAsync(import, Ct);
    }

    await using var reader = seed.Organizations.ContextOf(seed.Organizations.FirstId);
    var read = (await Repository(reader, seed.Organizations.FirstId).FindAsync(id, Ct)).ShouldNotBeNull();
    read.NextPage.ShouldBe(2);
    read.ImportedCommits.ShouldBe(42);
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task Update_NotReadInThisScope_ShouldThrow(DatabaseProvider provider)
  {
    var seed = await AddRepositoriesAsync(database, provider, Ct);
    await using var context = seed.Organizations.ContextOf(seed.Organizations.FirstId);

    await Should.ThrowAsync<InvalidOperationException>(() => Repository(context, seed.Organizations.FirstId).UpdateAsync(CodecovImport.Start(seed.FirstRepository, "p", Now), Ct));
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task FindAndDelete_OtherOrganization_ShouldNeitherReadNorDelete(DatabaseProvider provider)
  {
    var seed = await AddRepositoriesAsync(database, provider, Ct);
    var second = await StartAsync(seed, seed.Organizations.SecondId, seed.SecondRepository, Now);

    await using (var first = seed.Organizations.ContextOf(seed.Organizations.FirstId))
    {
      var repository = Repository(first, seed.Organizations.FirstId);

      (await repository.FindAsync(second, Ct)).ShouldBeNull();
      (await repository.DeleteAsync(second, Ct)).ShouldBeFalse();
    }

    await using var owner = seed.Organizations.ContextOf(seed.Organizations.SecondId);
    var ownRepository = Repository(owner, seed.Organizations.SecondId);
    (await ownRepository.FindAsync(second, Ct)).ShouldNotBeNull();
    (await ownRepository.DeleteAsync(second, Ct)).ShouldBeTrue();
    (await ownRepository.FindAsync(second, Ct)).ShouldBeNull();
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task Purge_ShouldDeleteOnlyTheExpiredImportsOfTheOrganization(DatabaseProvider provider)
  {
    var seed = await AddRepositoriesAsync(database, provider, Ct);
    var old = await StartAsync(seed, seed.Organizations.FirstId, seed.FirstRepository, Now.AddDays(-8));
    var recent = await StartAsync(seed, seed.Organizations.FirstId, seed.FirstRepository, Now.AddDays(-1));
    var otherOld = await StartAsync(seed, seed.Organizations.SecondId, seed.SecondRepository, Now.AddDays(-8));

    await using (var first = seed.Organizations.ContextOf(seed.Organizations.FirstId))
    {
      (await Repository(first, seed.Organizations.FirstId).PurgeAsync(Now - CodecovImport.CredentialLifetime, Ct)).ShouldBe(1);
    }

    await using var reader = seed.Organizations.ContextOf(seed.Organizations.FirstId);
    (await Repository(reader, seed.Organizations.FirstId).FindAsync(old, Ct)).ShouldBeNull();
    (await Repository(reader, seed.Organizations.FirstId).FindAsync(recent, Ct)).ShouldNotBeNull();

    await using var other = seed.Organizations.ContextOf(seed.Organizations.SecondId);
    (await Repository(other, seed.Organizations.SecondId).FindAsync(otherOld, Ct)).ShouldNotBeNull();
  }

  private static CodecovImportRepository Repository(CodeCoverageDbContext context, long organizationId)
  {
    var signal = new JobWakeUpSignal(TimeProvider.System);

    return new CodecovImportRepository(context, TestRepositories.Queue(context, organizationId, signal), signal);
  }

  private static async Task<long> StartAsync(BranchSeed seed, long organizationId, long repositoryId, DateTimeOffset at)
  {
    await using var context = seed.Organizations.ContextOf(organizationId);
    var import = CodecovImport.Start(repositoryId, "protected", at);

    await Repository(context, organizationId).StartAsync(import, Ct);

    return import.Id;
  }

  #endregion
}
