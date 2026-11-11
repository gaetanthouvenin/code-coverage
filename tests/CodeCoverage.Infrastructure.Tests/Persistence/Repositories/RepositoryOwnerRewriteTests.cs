// ------------------------------------------------------------------------------------------------
// <copyright file="RepositoryOwnerRewriteTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Globalization;

using Microsoft.EntityFrameworkCore;

using CodeCoverage.Infrastructure.Persistence;
using CodeCoverage.Infrastructure.Tests.Support;

using static CodeCoverage.Infrastructure.Tests.Support.RepositorySeeder;

namespace CodeCoverage.Infrastructure.Tests.Persistence.Repositories;

// The rewrite of the owner of the repositories of an organization whose account was renamed.
[Collection(DatabaseCollection.Name)]
public sealed class RepositoryOwnerRewriteTests(DatabaseFixture database)
{
  #region Properties

  private static CancellationToken Ct => TestContext.Current.CancellationToken;

  #endregion

  #region Methods

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task RewriteOwner_ShouldRewriteTheRepositoriesOfTheOrganizationOfTheScopeOnly(DatabaseProvider provider)
  {
    var organizations = await SeededAsync(provider, 3);

    (await RewriteAllAsync(organizations, organizations.FirstId, "new-octo", batchSize: 100)).ShouldBe(1);

    (await OwnersAsync(organizations, organizations.FirstId)).ShouldBe(["new-octo", "new-octo", "new-octo"]);
    // The other organization holds repositories of the same former owner: none is touched.
    (await OwnersAsync(organizations, organizations.SecondId)).ShouldBe(["octo", "octo", "octo"]);
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task RewriteOwner_ShouldGoThroughEveryRepositoryBatchAfterBatch(DatabaseProvider provider)
  {
    var organizations = await SeededAsync(provider, 7);

    // 7 repositories by 3: three batches that write, then the reading that finds none left.
    (await RewriteAllAsync(organizations, organizations.FirstId, "new-octo", batchSize: 3)).ShouldBe(3);

    (await OwnersAsync(organizations, organizations.FirstId)).ShouldAllBe(static owner => owner == "new-octo");
    (await OwnersAsync(organizations, organizations.FirstId)).Count.ShouldBe(7);
  }

  // One batch only: what comes after it is left for the next call.
  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task RewriteOwner_OneBatch_ShouldStopAtTheIdentifierItReturns(DatabaseProvider provider)
  {
    var organizations = await SeededAsync(provider, 5);
    long? last;

    await using (var context = organizations.ContextOf(organizations.FirstId))
    {
      last = await TestRepositories.Repositories(context).RewriteOwnerAsync("new-octo", 0, 2, Ct);
    }

    await using var reader = organizations.ContextOf(organizations.FirstId);
    var rows = await reader.Repositories.AsNoTracking().OrderBy(static r => r.Id).Select(static r => new { r.Id, r.Owner }).ToListAsync(Ct);

    last.ShouldBe(rows[1].Id);
    rows.Select(static r => r.Owner).ShouldBe(["new-octo", "new-octo", "octo", "octo", "octo"]);
  }

  // A rename that only changes the case is a rename: the collation of the database must not hide it.
  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task RewriteOwner_LoginThatOnlyChangesItsCase_ShouldBeWritten(DatabaseProvider provider)
  {
    var organizations = await SeededAsync(provider, 2);

    await RewriteAllAsync(organizations, organizations.FirstId, "OCTO", batchSize: 100);

    (await OwnersAsync(organizations, organizations.FirstId)).ShouldBe(["OCTO", "OCTO"]);
  }

  // The owner is a copy for display: neither the version nor the names of the rows move, inactive rows included,
  // and running the rewrite again changes nothing.
  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task RewriteOwner_ShouldLeaveTheRestOfTheRowsAndBeRunAgain(DatabaseProvider provider)
  {
    var organizations = await SeededAsync(provider, 2);

    await using (var writer = organizations.ContextOf(organizations.FirstId))
    {
      var repositories = TestRepositories.Repositories(writer);
      var inactive = (await repositories.FindByExternalIdAsync("first-1", Ct)).ShouldNotBeNull();

      inactive.Deactivate(Now.AddMinutes(1));
      await repositories.UpdateAsync(inactive, Ct);
    }

    var before = await RowsAsync(organizations);

    await RewriteAllAsync(organizations, organizations.FirstId, "new-octo", batchSize: 100);
    await RewriteAllAsync(organizations, organizations.FirstId, "new-octo", batchSize: 1);

    var after = await RowsAsync(organizations);

    after.Select(static r => r.Owner).ShouldAllBe(static owner => owner == "new-octo");
    after.Select(static r => (r.Id, r.Name, r.NormalizedName, r.Namespace, r.IsActive, r.Version, r.UpdatedAt))
         .ShouldBe(before.Select(static r => (r.Id, r.Name, r.NormalizedName, r.Namespace, r.IsActive, r.Version, r.UpdatedAt)));

    // A repository read before the rewrite is still saved: its version did not move.
    await using var context = organizations.ContextOf(organizations.FirstId);
    var found = (await TestRepositories.Repositories(context).FindByExternalIdAsync("first-0", Ct)).ShouldNotBeNull();

    found.Owner.ShouldBe("new-octo");
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task RewriteOwner_OrganizationWithoutRepositories_ShouldReturnNull(DatabaseProvider provider)
  {
    var organizations = await TwoOrganizationDatabase.CreateAsync(database, provider, Ct);

    await using var context = organizations.ContextOf(organizations.FirstId);

    (await TestRepositories.Repositories(context).RewriteOwnerAsync("new-octo", 0, 10, Ct)).ShouldBeNull();
  }

  [Fact]
  public async Task RewriteOwner_InvalidArguments_ShouldThrow()
  {
    await using var context = DatabaseFixture.CreateContext(Enum.GetValues<DatabaseProvider>()[0], "Server=localhost;Database=unused");
    var repositories = TestRepositories.Repositories(context);

    await Should.ThrowAsync<ArgumentException>(() => repositories.RewriteOwnerAsync("not a login", 0, 10, Ct));
    await Should.ThrowAsync<ArgumentException>(() => repositories.RewriteOwnerAsync("~stale-1001", 0, 10, Ct));
    await Should.ThrowAsync<ArgumentException>(() => repositories.RewriteOwnerAsync(null!, 0, 10, Ct));
    await Should.ThrowAsync<ArgumentOutOfRangeException>(() => repositories.RewriteOwnerAsync("octo", -1, 10, Ct));
    await Should.ThrowAsync<ArgumentOutOfRangeException>(() => repositories.RewriteOwnerAsync("octo", 0, 0, Ct));
  }

  private static async Task<int> RewriteAllAsync(TwoOrganizationDatabase organizations, long organizationId, string owner, int batchSize)
  {
    await using var context = organizations.ContextOf(organizationId);
    var repositories = TestRepositories.Repositories(context);
    var batches = 0;
    long? last = 0;

    while (last is { } afterId)
    {
      last = await repositories.RewriteOwnerAsync(owner, afterId, batchSize, Ct);
      batches += last is null ? 0 : 1;
    }

    return batches;
  }

  private static async Task<IReadOnlyList<string>> OwnersAsync(TwoOrganizationDatabase organizations, long organizationId)
  {
    await using var context = organizations.ContextOf(organizationId);

    return await context.Repositories.AsNoTracking().OrderBy(static r => r.Id).Select(static r => r.Owner).ToListAsync(Ct);
  }

  private static async Task<IReadOnlyList<Domain.Repositories.TrackedRepository>> RowsAsync(TwoOrganizationDatabase organizations)
  {
    await using var context = organizations.ContextOf(organizations.FirstId);

    return await context.Repositories.AsNoTracking().OrderBy(static r => r.Id).ToListAsync(Ct);
  }

  // Both organizations track repositories of an owner with the same login, so that only the filter of the
  // organization tells them apart.
  private async Task<TwoOrganizationDatabase> SeededAsync(DatabaseProvider provider, int count)
  {
    var organizations = await TwoOrganizationDatabase.CreateAsync(database, provider, Ct);
    await AddInstallationsAsync(organizations, Ct);

    await using var first = organizations.ContextOf(organizations.FirstId);
    await using var second = organizations.ContextOf(organizations.SecondId);

    for (var i = 0; i < count; i++)
    {
      var index = i.ToString(CultureInfo.InvariantCulture);

      await AddRepositoryAsync(first, FirstInstallation, "first-" + index, "octo/repo-" + index, Ct);
    }

    for (var i = 0; i < 3; i++)
    {
      var index = i.ToString(CultureInfo.InvariantCulture);

      await AddRepositoryAsync(second, SecondInstallation, "second-" + index, "octo/repo-" + index, Ct);
    }

    return organizations;
  }

  #endregion
}
