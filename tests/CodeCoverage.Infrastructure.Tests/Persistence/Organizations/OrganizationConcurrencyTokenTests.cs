// ------------------------------------------------------------------------------------------------
// <copyright file="OrganizationConcurrencyTokenTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

using CodeCoverage.Application.Exceptions;
using CodeCoverage.Application.Exceptions.ErrorCodes;
using CodeCoverage.Infrastructure.Persistence;
using CodeCoverage.Infrastructure.Persistence.Organizations;
using CodeCoverage.Infrastructure.Settings;
using CodeCoverage.Infrastructure.Tests.Support;

namespace CodeCoverage.Infrastructure.Tests.Persistence.Organizations;

// The attack the save guard cannot see (ADR-013): the shadow column written by hand with the current
// organization. The guard accepts it; only the column in the WHERE clause keeps the other row out of reach.
[Collection(DatabaseCollection.Name)]
public sealed class OrganizationConcurrencyTokenTests(DatabaseFixture database)
{
  #region Fields

  private const string Key = "Notifications:StatusContextPrefix";

  // Not held by the first organization: without the token, the forged UPDATE moves the row instead of hitting the unique index.
  private const string ForgedKey = "Notifications:CommentsEnabled";

  private const string AddedKey = "Notifications:StatusesEnabled";

  private const string FirstValue = "first";

  private const string SecondValue = "second";

  private const string NewValue = "changed";

  private const long UnknownId = 987654321;

  private static readonly DateTimeOffset Now = new(2026, 11, 9, 8, 0, 0, TimeSpan.Zero);

  #endregion

  #region Properties

  private static CancellationToken Ct => TestContext.Current.CancellationToken;

  #endregion

  #region Methods

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task SaveChanges_RemovedRowForgedBeforeAttaching_ShouldLeaveOtherOrganizationRow(DatabaseProvider provider)
  {
    var organizations = await SeededAsync(provider);
    var id = await RowIdAsync(organizations, organizations.SecondId);
    await using var first = organizations.ContextOf(organizations.FirstId);
    var row = ExistingRow(id);
    Shadow(first, row).CurrentValue = organizations.FirstId;
    first.OrganizationSettings.Remove(row);

    await ShouldBeRefusedAsync(first);

    await ShouldBeIntactAsync(organizations);
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task SaveChanges_UpdatedRowForgedBeforeAttaching_ShouldLeaveOtherOrganizationRow(DatabaseProvider provider)
  {
    var organizations = await SeededAsync(provider);
    var id = await RowIdAsync(organizations, organizations.SecondId);
    await using var first = organizations.ContextOf(organizations.FirstId);
    var row = ExistingRow(id);
    Shadow(first, row).CurrentValue = organizations.FirstId;
    first.OrganizationSettings.Update(row);

    await ShouldBeRefusedAsync(first);

    await ShouldBeIntactAsync(organizations);
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task SaveChanges_RemovedRowForgedAfterAttaching_ShouldLeaveOtherOrganizationRow(DatabaseProvider provider)
  {
    var organizations = await SeededAsync(provider);
    var id = await RowIdAsync(organizations, organizations.SecondId);
    await using var first = organizations.ContextOf(organizations.FirstId);
    var row = ExistingRow(id);
    first.OrganizationSettings.Attach(row);
    Shadow(first, row).CurrentValue = organizations.FirstId;
    Shadow(first, row).OriginalValue = organizations.FirstId;
    first.OrganizationSettings.Remove(row);

    await ShouldBeRefusedAsync(first);

    await ShouldBeIntactAsync(organizations);
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task SaveChanges_UpdatedRowForgedAfterAttaching_ShouldLeaveOtherOrganizationRow(DatabaseProvider provider)
  {
    var organizations = await SeededAsync(provider);
    var id = await RowIdAsync(organizations, organizations.SecondId);
    await using var first = organizations.ContextOf(organizations.FirstId);
    var row = ExistingRow(id);
    first.OrganizationSettings.Attach(row);
    Shadow(first, row).CurrentValue = organizations.FirstId;
    Shadow(first, row).OriginalValue = organizations.FirstId;
    first.OrganizationSettings.Update(row);

    await ShouldBeRefusedAsync(first);

    await ShouldBeIntactAsync(organizations);
  }

  // Only the current value is written after attaching: the original one still says the row was never read here.
  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task SaveChanges_RemovedRowWithOnlyCurrentValueForged_ShouldLeaveOtherOrganizationRow(DatabaseProvider provider)
  {
    var organizations = await SeededAsync(provider);
    var id = await RowIdAsync(organizations, organizations.SecondId);
    await using var first = organizations.ContextOf(organizations.FirstId);
    var row = ExistingRow(id);
    first.OrganizationSettings.Attach(row);
    Shadow(first, row).CurrentValue = organizations.FirstId;
    first.OrganizationSettings.Remove(row);

    await ShouldBeRefusedAsync(first);

    await ShouldBeIntactAsync(organizations);
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task SaveChanges_UpdatedRowWithOnlyCurrentValueForged_ShouldLeaveOtherOrganizationRow(DatabaseProvider provider)
  {
    var organizations = await SeededAsync(provider);
    var id = await RowIdAsync(organizations, organizations.SecondId);
    await using var first = organizations.ContextOf(organizations.FirstId);
    var row = ExistingRow(id);
    first.OrganizationSettings.Attach(row);
    Shadow(first, row).CurrentValue = organizations.FirstId;
    first.OrganizationSettings.Update(row);

    await ShouldBeRefusedAsync(first);

    await ShouldBeIntactAsync(organizations);
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task SaveChanges_ForgedRowSavedSynchronously_ShouldLeaveOtherOrganizationRow(DatabaseProvider provider)
  {
    var organizations = await SeededAsync(provider);
    var id = await RowIdAsync(organizations, organizations.SecondId);
    await using var first = organizations.ContextOf(organizations.FirstId);
    var row = ExistingRow(id);
    Shadow(first, row).CurrentValue = organizations.FirstId;
    first.OrganizationSettings.Remove(row);

#pragma warning disable CA1849 // The synchronous overload is the subject of this test.
    Should.Throw<ConflictException>(() => first.SaveChanges()).ErrorCode.ShouldBe(GeneralErrors.ConcurrentChange);
#pragma warning restore CA1849

    await ShouldBeIntactAsync(organizations);
  }

  // A save that fails writes nothing: the row added beside the forged one does not stay.
  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task SaveChanges_ForgedRowBesideAddedRow_ShouldWriteNothing(DatabaseProvider provider)
  {
    var organizations = await SeededAsync(provider);
    var id = await RowIdAsync(organizations, organizations.SecondId);
    await using var first = organizations.ContextOf(organizations.FirstId);
    var row = ExistingRow(id);
    Shadow(first, row).CurrentValue = organizations.FirstId;
    first.OrganizationSettings.Remove(row);
    first.OrganizationSettings.Add(
      new StoredOrganizationSetting { Key = AddedKey, Value = NewValue, UpdatedAt = Now, UpdatedBy = "test" }
    );

    await ShouldBeRefusedAsync(first);

    await ShouldBeIntactAsync(organizations);
  }

  // The refusal is the same whether the key belongs to another organization or to nobody: it reveals nothing.
  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task SaveChanges_ForgedRow_ShouldBeRefusedLikeUnknownRow(DatabaseProvider provider)
  {
    var organizations = await SeededAsync(provider);
    var id = await RowIdAsync(organizations, organizations.SecondId);

    var foreign = await RefusalOfForgedRemoveAsync(organizations, id);
    var unknown = await RefusalOfForgedRemoveAsync(organizations, UnknownId);

    foreign.ErrorCode.ShouldBe(unknown.ErrorCode);
    foreign.Message.ShouldBe(unknown.Message);
    foreign.Message.ShouldNotContain("organization", Case.Insensitive);
    foreign.Message.ShouldNotContain(id.ToString(System.Globalization.CultureInfo.InvariantCulture));
    await ShouldBeIntactAsync(organizations);
  }

  // The one legitimate case: the row of the organization went away between the read and the save.
  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task SaveChanges_RowRemovedSinceRead_ShouldBeRefusedAsConflict(DatabaseProvider provider)
  {
    var organizations = await SeededAsync(provider);
    await using var stale = organizations.ContextOf(organizations.FirstId);
    (await stale.OrganizationSettings.SingleAsync(Ct)).Value = NewValue;

    await using (var other = organizations.ContextOf(organizations.FirstId))
    {
      await other.OrganizationSettings.ExecuteDeleteAsync(Ct);
    }

    await ShouldBeRefusedAsync(stale);

    (await ValueOfAsync(organizations, organizations.SecondId)).ShouldBe(SecondValue);
  }

  private static StoredOrganizationSetting ExistingRow(long id) =>
    new() { Id = id, Key = ForgedKey, Value = NewValue, UpdatedAt = Now, UpdatedBy = "test" };

  private static PropertyEntry<StoredOrganizationSetting, long> Shadow(CodeCoverageDbContext context, StoredOrganizationSetting row) =>
    context.Entry(row).Property<long>(OrganizationOwnedExtensions.ColumnName);

  private static async Task ShouldBeRefusedAsync(CodeCoverageDbContext context)
  {
    var exception = await Should.ThrowAsync<ConflictException>(() => context.SaveChangesAsync(Ct));

    exception.ErrorCode.ShouldBe(GeneralErrors.ConcurrentChange);
  }

  private static async Task<ConflictException> RefusalOfForgedRemoveAsync(TwoOrganizationDatabase organizations, long id)
  {
    await using var first = organizations.ContextOf(organizations.FirstId);
    var row = ExistingRow(id);
    Shadow(first, row).CurrentValue = organizations.FirstId;
    first.OrganizationSettings.Remove(row);

    return await Should.ThrowAsync<ConflictException>(() => first.SaveChangesAsync(Ct));
  }

  // Read under each organization: the other row still holds its value, and it was not moved.
  private static async Task ShouldBeIntactAsync(TwoOrganizationDatabase organizations)
  {
    (await ValueOfAsync(organizations, organizations.SecondId)).ShouldBe(SecondValue);
    (await ValueOfAsync(organizations, organizations.FirstId)).ShouldBe(FirstValue);

    await using var second = organizations.ContextOf(organizations.SecondId);
    (await second.OrganizationSettings.CountAsync(Ct)).ShouldBe(1);

    await using var first = organizations.ContextOf(organizations.FirstId);
    (await first.OrganizationSettings.CountAsync(Ct)).ShouldBe(1);
  }

  private async Task<TwoOrganizationDatabase> SeededAsync(DatabaseProvider provider)
  {
    var organizations = await TwoOrganizationDatabase.CreateAsync(database, provider, Ct);
    await organizations.AddSettingAsync(organizations.FirstId, Key, FirstValue, Ct);
    await organizations.AddSettingAsync(organizations.SecondId, Key, SecondValue, Ct);

    return organizations;
  }

  private static async Task<long> RowIdAsync(TwoOrganizationDatabase organizations, long organizationId)
  {
    await using var context = organizations.ContextOf(organizationId);

    return await context.OrganizationSettings.Select(static s => s.Id).SingleAsync(Ct);
  }

  private static async Task<string?> ValueOfAsync(TwoOrganizationDatabase organizations, long organizationId)
  {
    await using var context = organizations.ContextOf(organizationId);

    return await context.OrganizationSettings.Where(static s => s.Key == Key).Select(static s => s.Value).SingleOrDefaultAsync(Ct);
  }

  #endregion
}
