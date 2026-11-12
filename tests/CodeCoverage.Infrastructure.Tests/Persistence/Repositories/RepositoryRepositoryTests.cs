// ------------------------------------------------------------------------------------------------
// <copyright file="RepositoryRepositoryTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.EntityFrameworkCore;

using CodeCoverage.Application.Exceptions;
using CodeCoverage.Application.Exceptions.ErrorCodes;
using CodeCoverage.Domain.Repositories;
using CodeCoverage.Infrastructure.Persistence;
using CodeCoverage.Infrastructure.Persistence.Repositories;
using CodeCoverage.Infrastructure.Tests.Support;

using static CodeCoverage.Infrastructure.Tests.Support.RepositorySeeder;

namespace CodeCoverage.Infrastructure.Tests.Persistence.Repositories;

// Every method against two organizations, on both engines: the rows of the other one are neither read nor written.
[Collection(DatabaseCollection.Name)]
public sealed class RepositoryRepositoryTests(DatabaseFixture database)
{
  #region Fields

  private const string SharedSlug = "shared-org/shared-repo";

  private const string SharedExternalId = "4242";

  private const string NoOrganization = "has not entered";

  #endregion

  #region Properties

  private static CancellationToken Ct => TestContext.Current.CancellationToken;

  #endregion

  #region Methods

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task FindBySlug_ShouldBeCaseInsensitive(DatabaseProvider provider)
  {
    var organizations = await SeededAsync(provider);
    await using var context = organizations.ContextOf(organizations.FirstId);
    var id = await AddRepositoryAsync(context, FirstInstallation, "1", "Octo-Org/Hello-World", Ct);

    var found = await TestRepositories.Repositories(context).FindBySlugAsync(RepositorySlug.Parse("OCTO-org/hello-WORLD"), Ct);

    found.ShouldNotBeNull().Id.ShouldBe(id);
    found.Name.ShouldBe("Hello-World");
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task FindBySlug_OtherOwnerSameName_ShouldFindTheRowOfTheOrganization(DatabaseProvider provider)
  {
    var organizations = await SeededAsync(provider);
    await using var context = organizations.ContextOf(organizations.FirstId);
    var id = await AddRepositoryAsync(context, FirstInstallation, "1", "old-owner/repo", Ct);

    var found = await TestRepositories.Repositories(context).FindBySlugAsync(RepositorySlug.Parse("new-owner/repo"), Ct);

    found.ShouldNotBeNull().Id.ShouldBe(id);
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task FindBySlug_SameNameInOtherOrganization_ShouldReturnOwnRow(DatabaseProvider provider)
  {
    var organizations = await SeededAsync(provider);
    var (firstId, secondId) = await AddToBothAsync(organizations);

    await using var first = organizations.ContextOf(organizations.FirstId);
    await using var second = organizations.ContextOf(organizations.SecondId);
    var slug = RepositorySlug.Parse(SharedSlug);

    (await TestRepositories.Repositories(first).FindBySlugAsync(slug, Ct)).ShouldNotBeNull().Id.ShouldBe(firstId);
    (await TestRepositories.Repositories(second).FindBySlugAsync(slug, Ct)).ShouldNotBeNull().Id.ShouldBe(secondId);
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task FindBySlug_OnlyInOtherOrganization_ShouldReturnNull(DatabaseProvider provider)
  {
    var organizations = await SeededAsync(provider);

    await using (var second = organizations.ContextOf(organizations.SecondId))
    {
      await AddRepositoryAsync(second, SecondInstallation, SharedExternalId, SharedSlug, Ct);
    }

    await using var first = organizations.ContextOf(organizations.FirstId);

    (await TestRepositories.Repositories(first).FindBySlugAsync(RepositorySlug.Parse(SharedSlug), Ct)).ShouldBeNull();
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task FindBySlug_SameNameInOtherNamespace_ShouldReturnOwnRow(DatabaseProvider provider)
  {
    var organizations = await SeededAsync(provider);
    await using var context = organizations.ContextOf(organizations.FirstId);
    var inA = await AddRepositoryAsync(context, FirstInstallation, "1", "group/team-a/repo", Ct);
    var inB = await AddRepositoryAsync(context, FirstInstallation, "2", "group/team-b/repo", Ct);
    var repositories = TestRepositories.Repositories(context);

    (await repositories.FindBySlugAsync(RepositorySlug.Parse("group/team-a/repo"), Ct)).ShouldNotBeNull().Id.ShouldBe(inA);
    (await repositories.FindBySlugAsync(RepositorySlug.Parse("group/team-b/repo"), Ct)).ShouldNotBeNull().Id.ShouldBe(inB);
    (await repositories.FindBySlugAsync(RepositorySlug.Parse("group/repo"), Ct)).ShouldBeNull();
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task Find_OtherOrganization_ShouldReturnNull(DatabaseProvider provider)
  {
    var organizations = await SeededAsync(provider);
    var (firstId, secondId) = await AddToBothAsync(organizations);

    await using var first = organizations.ContextOf(organizations.FirstId);
    var repositories = TestRepositories.Repositories(first);

    (await repositories.FindAsync(firstId, Ct)).ShouldNotBeNull();
    (await repositories.FindAsync(secondId, Ct)).ShouldBeNull();
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task FindByExternalId_ShouldBeCaseInsensitive(DatabaseProvider provider)
  {
    var organizations = await SeededAsync(provider);
    await using var context = organizations.ContextOf(organizations.FirstId);
    var id = await AddRepositoryAsync(context, FirstInstallation, "{ABC-123}", "owner/repo", Ct);

    var found = await TestRepositories.Repositories(context).FindByExternalIdAsync("{abc-123}", Ct);

    found.ShouldNotBeNull().Id.ShouldBe(id);
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task FindByExternalId_OtherOrganization_ShouldReturnNull(DatabaseProvider provider)
  {
    var organizations = await SeededAsync(provider);

    await using (var second = organizations.ContextOf(organizations.SecondId))
    {
      await AddRepositoryAsync(second, SecondInstallation, SharedExternalId, SharedSlug, Ct);
    }

    await using var first = organizations.ContextOf(organizations.FirstId);

    (await TestRepositories.Repositories(first).FindByExternalIdAsync(SharedExternalId, Ct)).ShouldBeNull();
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task List_ShouldExcludeOtherOrganization(DatabaseProvider provider)
  {
    var organizations = await SeededAsync(provider);
    var (firstId, _) = await AddToBothAsync(organizations);

    await using var first = organizations.ContextOf(organizations.FirstId);

    var listed = await TestRepositories.Repositories(first).ListAsync(Ct);

    listed.ShouldHaveSingleItem().Id.ShouldBe(firstId);
  }

  // M3.06: a case-insensitive "name contains", active repositories only, within the organization of the scope.
  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task SearchActive_ShouldMatchTheNameWhateverItsCaseAndSkipInactive(DatabaseProvider provider)
  {
    var organizations = await SeededAsync(provider);
    await AddToBothAsync(organizations);

    long app;
    long inactive;

    await using (var context = organizations.ContextOf(organizations.FirstId))
    {
      app = await AddRepositoryAsync(context, FirstInstallation, "7001", "octo-org/My-App", Ct);
      await AddRepositoryAsync(context, FirstInstallation, "7002", "octo-org/tools", Ct);
      inactive = await AddRepositoryAsync(context, FirstInstallation, "7003", "octo-org/old-app", Ct);
      var old = await context.Repositories.SingleAsync(r => r.Id == inactive, Ct);
      old.Deactivate(Now);
      await context.SaveChangesAsync(Ct);
    }

    await using var first = organizations.ContextOf(organizations.FirstId);
    var repositories = TestRepositories.Repositories(first);

    (await repositories.SearchActiveAsync("APP", Ct)).Select(static r => r.Id).ShouldBe([app]);
    (await repositories.SearchActiveAsync("%", Ct)).ShouldBeEmpty();
    (await repositories.SearchActiveAsync(null, Ct)).ShouldNotContain(r => r.Id == inactive);

    await using var second = organizations.ContextOf(organizations.SecondId);
    (await TestRepositories.Repositories(second).SearchActiveAsync("app", Ct)).ShouldBeEmpty();
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task List_ShouldNotTrackTheRows(DatabaseProvider provider)
  {
    var organizations = await SeededAsync(provider);
    await AddToBothAsync(organizations);

    await using var first = organizations.ContextOf(organizations.FirstId);
    var repositories = TestRepositories.Repositories(first);
    var listed = (await repositories.ListAsync(Ct)).Single();

    await Should.ThrowAsync<InvalidOperationException>(() => repositories.UpdateAsync(listed, Ct));
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task Add_SameExternalIdInTwoOrganizations_ShouldKeepBoth(DatabaseProvider provider)
  {
    var organizations = await SeededAsync(provider);

    var (firstId, secondId) = await AddToBothAsync(organizations);

    firstId.ShouldNotBe(secondId);
    await using var first = organizations.ContextOf(organizations.FirstId);
    await using var second = organizations.ContextOf(organizations.SecondId);
    (await TestRepositories.Repositories(first).FindByExternalIdAsync(SharedExternalId, Ct)).ShouldNotBeNull().Id.ShouldBe(firstId);
    (await TestRepositories.Repositories(second).FindByExternalIdAsync(SharedExternalId, Ct)).ShouldNotBeNull().Id.ShouldBe(secondId);
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task Add_WithoutOrganization_ShouldThrowAndWriteNothing(DatabaseProvider provider)
  {
    var organizations = await SeededAsync(provider);

    await using (var context = organizations.ContextWithoutOrganization())
    {
      var repository = NewRepository(FirstInstallation, SharedExternalId, SharedSlug);

      var exception = await Should.ThrowAsync<InvalidOperationException>(
        () => TestRepositories.Repositories(context).AddAsync(repository, Ct));

      exception.Message.ShouldContain(NoOrganization);
    }

    await ShouldHoldNoRowAsync(organizations, organizations.FirstId);
    await ShouldHoldNoRowAsync(organizations, organizations.SecondId);
  }

  // A concurrent first upload lands here: the loser reads the row of the winner and goes on.
  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task Add_SameNameTwice_ShouldThrowConflictAndLeaveTheScopeUsable(DatabaseProvider provider)
  {
    var organizations = await SeededAsync(provider);
    await using var context = organizations.ContextOf(organizations.FirstId);
    await AddRepositoryAsync(context, FirstInstallation, "1", "Owner/Repo", Ct);
    var repositories = TestRepositories.Repositories(context);
    var duplicate = NewRepository(FirstInstallation, "2", "owner/repo");

    var exception = await Should.ThrowAsync<ConflictException>(() => repositories.AddAsync(duplicate, Ct));

    exception.ErrorCode.ShouldBe(GeneralErrors.ConcurrentChange);
    await repositories.AddAsync(NewRepository(FirstInstallation, "3", "owner/other"), Ct);
    (await repositories.ListAsync(Ct)).Count.ShouldBe(2);
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task Add_SameExternalIdTwice_ShouldThrowConflict(DatabaseProvider provider)
  {
    var organizations = await SeededAsync(provider);
    await using var context = organizations.ContextOf(organizations.FirstId);
    await AddRepositoryAsync(context, FirstInstallation, "ABC", "owner/first", Ct);
    var duplicate = NewRepository(FirstInstallation, "abc", "owner/second");

    var exception = await Should.ThrowAsync<ConflictException>(() => TestRepositories.Repositories(context).AddAsync(duplicate, Ct));

    exception.ErrorCode.ShouldBe(GeneralErrors.ConcurrentChange);
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task Update_ReadInThisScope_ShouldSave(DatabaseProvider provider)
  {
    var organizations = await SeededAsync(provider);
    var (firstId, _) = await AddToBothAsync(organizations);

    await using (var context = organizations.ContextOf(organizations.FirstId))
    {
      var repositories = TestRepositories.Repositories(context);
      var repository = (await repositories.FindAsync(firstId, Ct)).ShouldNotBeNull();
      repository.Rename(RepositorySlug.Parse("shared-org/renamed"), Now.AddHours(1));

      await repositories.UpdateAsync(repository, Ct);
    }

    await using var reader = organizations.ContextOf(organizations.FirstId);
    var saved = (await TestRepositories.Repositories(reader).FindAsync(firstId, Ct)).ShouldNotBeNull();
    saved.Name.ShouldBe("renamed");
    saved.ExternalId.ShouldBe(SharedExternalId);
    saved.Version.ShouldBe(1);
  }

  // The acceptance of the micro-feature: optimistic concurrency through Version.
  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task Update_StaleVersion_ShouldThrowConflict(DatabaseProvider provider)
  {
    var organizations = await SeededAsync(provider);
    var (firstId, _) = await AddToBothAsync(organizations);

    await using var winner = organizations.ContextOf(organizations.FirstId);
    await using var loser = organizations.ContextOf(organizations.FirstId);
    var winnerRepositories = TestRepositories.Repositories(winner);
    var loserRepositories = TestRepositories.Repositories(loser);
    var winnerRow = (await winnerRepositories.FindAsync(firstId, Ct)).ShouldNotBeNull();
    var loserRow = (await loserRepositories.FindAsync(firstId, Ct)).ShouldNotBeNull();
    winnerRow.Rename(RepositorySlug.Parse("shared-org/winner"), Now.AddHours(1));
    loserRow.Rename(RepositorySlug.Parse("shared-org/loser"), Now.AddHours(2));
    await winnerRepositories.UpdateAsync(winnerRow, Ct);

    var exception = await Should.ThrowAsync<ConflictException>(() => loserRepositories.UpdateAsync(loserRow, Ct));

    exception.ErrorCode.ShouldBe(GeneralErrors.ConcurrentChange);
    await using var reader = organizations.ContextOf(organizations.FirstId);
    (await TestRepositories.Repositories(reader).FindAsync(firstId, Ct)).ShouldNotBeNull().Name.ShouldBe("winner");
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task Update_RowRemovedMeanwhile_ShouldThrowConflict(DatabaseProvider provider)
  {
    var organizations = await SeededAsync(provider);
    var (firstId, _) = await AddToBothAsync(organizations);

    await using var context = organizations.ContextOf(organizations.FirstId);
    var repositories = TestRepositories.Repositories(context);
    var repository = (await repositories.FindAsync(firstId, Ct)).ShouldNotBeNull();
    repository.Rename(RepositorySlug.Parse("shared-org/renamed"), Now.AddHours(1));

    await using (var other = organizations.ContextOf(organizations.FirstId))
    {
      await other.Repositories.Where(r => r.Id == firstId).ExecuteDeleteAsync(Ct);
    }

    var exception = await Should.ThrowAsync<ConflictException>(() => repositories.UpdateAsync(repository, Ct));

    exception.ErrorCode.ShouldBe(GeneralErrors.ConcurrentChange);
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task Update_InstanceReadByOtherOrganization_ShouldThrowAndWriteNothing(DatabaseProvider provider)
  {
    var organizations = await SeededAsync(provider);
    var (_, secondId) = await AddToBothAsync(organizations);

    await using (var second = organizations.ContextOf(organizations.SecondId))
    {
      var foreign = (await TestRepositories.Repositories(second).FindAsync(secondId, Ct)).ShouldNotBeNull();
      foreign.Rename(RepositorySlug.Parse("shared-org/stolen"), Now.AddHours(1));

      await using var first = organizations.ContextOf(organizations.FirstId);

      await Should.ThrowAsync<InvalidOperationException>(() => TestRepositories.Repositories(first).UpdateAsync(foreign, Ct));
    }

    await using var reader = organizations.ContextOf(organizations.SecondId);
    var stored = (await TestRepositories.Repositories(reader).FindAsync(secondId, Ct)).ShouldNotBeNull();
    stored.Name.ShouldBe("shared-repo");
    stored.Version.ShouldBe(0);
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task Find_WithoutOrganization_ShouldThrow(DatabaseProvider provider)
  {
    var organizations = await SeededAsync(provider);
    await using var context = organizations.ContextWithoutOrganization();

    var exception = await Should.ThrowAsync<InvalidOperationException>(() => TestRepositories.Repositories(context).FindAsync(1, Ct));

    exception.Message.ShouldContain(NoOrganization);
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task Find_NoYamlYet_ShouldHaveNoYamlSha(DatabaseProvider provider)
  {
    var organizations = await SeededAsync(provider);
    var (firstId, _) = await AddToBothAsync(organizations);

    await using var context = organizations.ContextOf(organizations.FirstId);

    (await TestRepositories.Repositories(context).FindAsync(firstId, Ct)).ShouldNotBeNull().DefaultBranchYamlSha.ShouldBeNull();
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task Find_StoredYamlSha_ShouldReadTheSha(DatabaseProvider provider)
  {
    var organizations = await SeededAsync(provider);
    var (firstId, _) = await AddToBothAsync(organizations);
    await SetStoredYamlShaAsync(organizations, firstId, Head.Value);

    await using var context = organizations.ContextOf(organizations.FirstId);

    (await TestRepositories.Repositories(context).FindAsync(firstId, Ct)).ShouldNotBeNull().DefaultBranchYamlSha.ShouldBe(Head);
  }

  // The invariant of GitSha holds on the way back too: a corrupt column fails the read, it never yields a value.
  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task Find_InvalidStoredYamlSha_ShouldThrow(DatabaseProvider provider)
  {
    var organizations = await SeededAsync(provider);
    var (firstId, _) = await AddToBothAsync(organizations);
    await SetStoredYamlShaAsync(organizations, firstId, "not-a-sha");

    await using var context = organizations.ContextOf(organizations.FirstId);

    await Should.ThrowAsync<ArgumentException>(() => TestRepositories.Repositories(context).FindAsync(firstId, Ct));
  }

  // Raw on purpose: the Domain has no setter yet, and the model cannot write a text that is not a SHA.
  // Double quotes are valid on both engines.
  private static async Task SetStoredYamlShaAsync(TwoOrganizationDatabase organizations, long id, string value)
  {
    await using var context = organizations.ContextOf(organizations.FirstId);

    await context.Database.ExecuteSqlAsync($"""UPDATE "Repositories" SET "DefaultBranchYamlSha" = {value} WHERE "Id" = {id}""", Ct);
  }

  private static async Task ShouldHoldNoRowAsync(TwoOrganizationDatabase organizations, long organizationId)
  {
    await using var reader = organizations.ContextOf(organizationId);

    (await TestRepositories.Repositories(reader).ListAsync(Ct)).ShouldBeEmpty();
  }

  private static async Task<(long FirstId, long SecondId)> AddToBothAsync(TwoOrganizationDatabase organizations)
  {
    await using var first = organizations.ContextOf(organizations.FirstId);
    await using var second = organizations.ContextOf(organizations.SecondId);

    var firstId = await AddRepositoryAsync(first, FirstInstallation, SharedExternalId, SharedSlug, Ct);
    var secondId = await AddRepositoryAsync(second, SecondInstallation, SharedExternalId, SharedSlug, Ct);

    return (firstId, secondId);
  }

  private async Task<TwoOrganizationDatabase> SeededAsync(DatabaseProvider provider)
  {
    var organizations = await TwoOrganizationDatabase.CreateAsync(database, provider, Ct);
    await AddInstallationsAsync(organizations, Ct);

    return organizations;
  }

  #endregion
}
