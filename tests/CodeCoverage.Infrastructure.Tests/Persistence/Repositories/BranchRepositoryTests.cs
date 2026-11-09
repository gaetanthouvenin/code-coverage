// ------------------------------------------------------------------------------------------------
// <copyright file="BranchRepositoryTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.EntityFrameworkCore;

using CodeCoverage.Application.Exceptions;
using CodeCoverage.Application.Exceptions.ErrorCodes;
using CodeCoverage.Application.Features.Repositories;
using CodeCoverage.Domain.Repositories;
using CodeCoverage.Infrastructure.Persistence;
using CodeCoverage.Infrastructure.Persistence.Repositories;
using CodeCoverage.Infrastructure.Tests.Support;

using static CodeCoverage.Infrastructure.Tests.Support.RepositorySeeder;

namespace CodeCoverage.Infrastructure.Tests.Persistence.Repositories;

// Every method against two organizations, on both engines: the rows of the other one are neither read nor written.
[Collection(DatabaseCollection.Name)]
public sealed class BranchRepositoryTests(DatabaseFixture database)
{
  #region Fields

  private const string BranchName = "main";

  #endregion

  #region Properties

  private static CancellationToken Ct => TestContext.Current.CancellationToken;

  #endregion

  #region Methods

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task Find_Added_ShouldReturnTheBranch(DatabaseProvider provider)
  {
    var seed = await SeededAsync(provider);
    await using var context = seed.Organizations.ContextOf(seed.Organizations.FirstId);
    var branches = new BranchRepository(context);
    await branches.AddAsync(Branch.Create(seed.FirstRepository, BranchName, Head, Now), Ct);

    var found = await branches.FindAsync(seed.FirstRepository, BranchName, Ct);

    found.ShouldNotBeNull().HeadSha.ShouldBe(Head);
    found.CoveredCommitId.ShouldBeNull();
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task Find_StoredInUpperCase_ShouldReadTheNormalizedSha(DatabaseProvider provider)
  {
    var seed = await SeededAsync(provider);
    await AddBranchAsync(seed.Organizations, seed.Organizations.FirstId, seed.FirstRepository);
    await SetStoredHeadAsync(seed, Head.Value.ToUpperInvariant());

    await using var context = seed.Organizations.ContextOf(seed.Organizations.FirstId);

    (await new BranchRepository(context).FindAsync(seed.FirstRepository, BranchName, Ct)).ShouldNotBeNull().HeadSha.ShouldBe(Head);
  }

  // The invariant of GitSha holds on the way back too: a corrupt column fails the read, it never yields a value.
  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task Find_InvalidStoredSha_ShouldThrow(DatabaseProvider provider)
  {
    var seed = await SeededAsync(provider);
    await AddBranchAsync(seed.Organizations, seed.Organizations.FirstId, seed.FirstRepository);
    await SetStoredHeadAsync(seed, "not-a-sha");

    await using var context = seed.Organizations.ContextOf(seed.Organizations.FirstId);

    await Should.ThrowAsync<ArgumentException>(() => new BranchRepository(context).FindAsync(seed.FirstRepository, BranchName, Ct));
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task Find_OtherOrganization_ShouldReturnNull(DatabaseProvider provider)
  {
    var seed = await SeededAsync(provider);
    await AddBranchAsync(seed.Organizations, seed.Organizations.SecondId, seed.SecondRepository);

    await using var first = seed.Organizations.ContextOf(seed.Organizations.FirstId);

    (await new BranchRepository(first).FindAsync(seed.SecondRepository, BranchName, Ct)).ShouldBeNull();
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task List_OtherOrganization_ShouldBeEmpty(DatabaseProvider provider)
  {
    var seed = await SeededAsync(provider);
    await AddBranchAsync(seed.Organizations, seed.Organizations.SecondId, seed.SecondRepository);

    await using var first = seed.Organizations.ContextOf(seed.Organizations.FirstId);

    (await new BranchRepository(first).ListAsync(seed.SecondRepository, Ct)).ShouldBeEmpty();
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task List_ShouldSortByName(DatabaseProvider provider)
  {
    var seed = await SeededAsync(provider);
    await using var context = seed.Organizations.ContextOf(seed.Organizations.FirstId);
    var branches = new BranchRepository(context);
    await branches.AddAsync(Branch.Create(seed.FirstRepository, "zeta", Head, Now), Ct);
    await branches.AddAsync(Branch.Create(seed.FirstRepository, "alpha", Head, Now), Ct);

    var listed = await branches.ListAsync(seed.FirstRepository, Ct);

    listed.Select(static b => b.Name).ShouldBe(["alpha", "zeta"]);
  }

  // The primary key does not hold the organization: without the check, the name would be taken in the other one.
  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task Add_RepositoryOfOtherOrganization_ShouldThrowNotFoundAndWriteNothing(DatabaseProvider provider)
  {
    var seed = await SeededAsync(provider);

    await using (var first = seed.Organizations.ContextOf(seed.Organizations.FirstId))
    {
      var branch = Branch.Create(seed.SecondRepository, BranchName, Head, Now);

      var exception = await Should.ThrowAsync<NotFoundException>(() => new BranchRepository(first).AddAsync(branch, Ct));

      exception.ErrorCode.ShouldBe(RepositoryErrors.NotFound);
    }

    await using var second = seed.Organizations.ContextOf(seed.Organizations.SecondId);
    var branches = new BranchRepository(second);
    (await branches.ListAsync(seed.SecondRepository, Ct)).ShouldBeEmpty();
    await branches.AddAsync(Branch.Create(seed.SecondRepository, BranchName, Head, Now), Ct);
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task Add_UnknownRepository_ShouldThrowNotFound(DatabaseProvider provider)
  {
    var seed = await SeededAsync(provider);
    await using var first = seed.Organizations.ContextOf(seed.Organizations.FirstId);
    var branch = Branch.Create(long.MaxValue, BranchName, Head, Now);

    var exception = await Should.ThrowAsync<NotFoundException>(() => new BranchRepository(first).AddAsync(branch, Ct));

    exception.ErrorCode.ShouldBe(RepositoryErrors.NotFound);
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task Add_SameBranchTwice_ShouldThrowConflictAndLeaveTheScopeUsable(DatabaseProvider provider)
  {
    var seed = await SeededAsync(provider);
    await using var context = seed.Organizations.ContextOf(seed.Organizations.FirstId);
    var branches = new BranchRepository(context);
    await branches.AddAsync(Branch.Create(seed.FirstRepository, BranchName, Head, Now), Ct);
    var other = seed.Organizations.ContextOf(seed.Organizations.FirstId);

    await using (other)
    {
      var exception = await Should.ThrowAsync<ConflictException>(
        () => new BranchRepository(other).AddAsync(Branch.Create(seed.FirstRepository, BranchName, NewHead, Now), Ct));

      exception.ErrorCode.ShouldBe(GeneralErrors.ConcurrentChange);
      await new BranchRepository(other).AddAsync(Branch.Create(seed.FirstRepository, "develop", NewHead, Now), Ct);
    }
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task Update_ReadInThisScope_ShouldSave(DatabaseProvider provider)
  {
    var seed = await SeededAsync(provider);
    await AddBranchAsync(seed.Organizations, seed.Organizations.FirstId, seed.FirstRepository);

    await using (var context = seed.Organizations.ContextOf(seed.Organizations.FirstId))
    {
      var branches = new BranchRepository(context);
      var branch = (await branches.FindAsync(seed.FirstRepository, BranchName, Ct)).ShouldNotBeNull();
      branch.MoveHead(NewHead, Now.AddHours(1));

      await branches.UpdateAsync(branch, Ct);
    }

    await using var reader = seed.Organizations.ContextOf(seed.Organizations.FirstId);
    var saved = (await new BranchRepository(reader).FindAsync(seed.FirstRepository, BranchName, Ct)).ShouldNotBeNull();
    saved.HeadSha.ShouldBe(NewHead);
    saved.Version.ShouldBe(1);
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task Update_StaleVersion_ShouldThrowConflict(DatabaseProvider provider)
  {
    var seed = await SeededAsync(provider);
    await AddBranchAsync(seed.Organizations, seed.Organizations.FirstId, seed.FirstRepository);

    await using var winner = seed.Organizations.ContextOf(seed.Organizations.FirstId);
    await using var loser = seed.Organizations.ContextOf(seed.Organizations.FirstId);
    var winnerBranches = new BranchRepository(winner);
    var loserBranches = new BranchRepository(loser);
    var winnerRow = (await winnerBranches.FindAsync(seed.FirstRepository, BranchName, Ct)).ShouldNotBeNull();
    var loserRow = (await loserBranches.FindAsync(seed.FirstRepository, BranchName, Ct)).ShouldNotBeNull();
    winnerRow.MoveHead(NewHead, Now.AddHours(1));
    loserRow.MoveHead(NewHead, Now.AddHours(2));
    await winnerBranches.UpdateAsync(winnerRow, Ct);

    var exception = await Should.ThrowAsync<ConflictException>(() => loserBranches.UpdateAsync(loserRow, Ct));

    exception.ErrorCode.ShouldBe(GeneralErrors.ConcurrentChange);
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task Update_InstanceReadByOtherOrganization_ShouldThrowAndWriteNothing(DatabaseProvider provider)
  {
    var seed = await SeededAsync(provider);
    await AddBranchAsync(seed.Organizations, seed.Organizations.SecondId, seed.SecondRepository);

    await using (var second = seed.Organizations.ContextOf(seed.Organizations.SecondId))
    {
      var foreign = (await new BranchRepository(second).FindAsync(seed.SecondRepository, BranchName, Ct)).ShouldNotBeNull();
      foreign.MoveHead(NewHead, Now.AddHours(1));

      await using var first = seed.Organizations.ContextOf(seed.Organizations.FirstId);

      await Should.ThrowAsync<InvalidOperationException>(() => new BranchRepository(first).UpdateAsync(foreign, Ct));
    }

    await ShouldStayAsync(seed);
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task Remove_Existing_ShouldDeleteTheRow(DatabaseProvider provider)
  {
    var seed = await SeededAsync(provider);
    await AddBranchAsync(seed.Organizations, seed.Organizations.FirstId, seed.FirstRepository);

    await using var context = seed.Organizations.ContextOf(seed.Organizations.FirstId);
    var branches = new BranchRepository(context);

    await branches.RemoveAsync(seed.FirstRepository, BranchName, Ct);

    (await branches.FindAsync(seed.FirstRepository, BranchName, Ct)).ShouldBeNull();
  }

  // A redelivered "push deleted" webhook is absorbed.
  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task Remove_Unknown_ShouldDoNothing(DatabaseProvider provider)
  {
    var seed = await SeededAsync(provider);
    await using var context = seed.Organizations.ContextOf(seed.Organizations.FirstId);

    await new BranchRepository(context).RemoveAsync(seed.FirstRepository, "gone", Ct);

    (await context.Branches.CountAsync(Ct)).ShouldBe(0);
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task Remove_OtherOrganization_ShouldLeaveTheRow(DatabaseProvider provider)
  {
    var seed = await SeededAsync(provider);
    await AddBranchAsync(seed.Organizations, seed.Organizations.SecondId, seed.SecondRepository);

    await using (var first = seed.Organizations.ContextOf(seed.Organizations.FirstId))
    {
      await new BranchRepository(first).RemoveAsync(seed.SecondRepository, BranchName, Ct);
    }

    await ShouldStayAsync(seed);
  }

  private static async Task AddBranchAsync(TwoOrganizationDatabase organizations, long organizationId, long repositoryId)
  {
    await using var context = organizations.ContextOf(organizationId);

    await new BranchRepository(context).AddAsync(Branch.Create(repositoryId, BranchName, Head, Now), Ct);
  }

  // Raw on purpose: the model cannot write a text that is not a SHA. Double quotes are valid on both engines.
  private static async Task SetStoredHeadAsync(BranchSeed seed, string value)
  {
    await using var context = seed.Organizations.ContextOf(seed.Organizations.FirstId);

    await context.Database.ExecuteSqlAsync($"""UPDATE "Branches" SET "HeadSha" = {value}""", Ct);
  }

  private static async Task ShouldStayAsync(BranchSeed seed)
  {
    await using var reader = seed.Organizations.ContextOf(seed.Organizations.SecondId);
    var stored = (await new BranchRepository(reader).FindAsync(seed.SecondRepository, BranchName, Ct)).ShouldNotBeNull();

    stored.HeadSha.ShouldBe(Head);
    stored.Version.ShouldBe(0);
  }

  private async Task<BranchSeed> SeededAsync(DatabaseProvider provider)
  {
    var organizations = await TwoOrganizationDatabase.CreateAsync(database, provider, Ct);
    await AddInstallationsAsync(organizations, Ct);

    await using var first = organizations.ContextOf(organizations.FirstId);
    await using var second = organizations.ContextOf(organizations.SecondId);
    var firstRepository = await AddRepositoryAsync(first, FirstInstallation, "1", "first-org/repo", Ct);
    var secondRepository = await AddRepositoryAsync(second, SecondInstallation, "2", "second-org/repo", Ct);

    return new BranchSeed(organizations, firstRepository, secondRepository);
  }

  #endregion
}
