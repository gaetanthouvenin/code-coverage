// ------------------------------------------------------------------------------------------------
// <copyright file="OrganizationWriteGuardTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.EntityFrameworkCore;

using CodeCoverage.Infrastructure.Persistence;
using CodeCoverage.Infrastructure.Persistence.Organizations;
using CodeCoverage.Infrastructure.Settings;
using CodeCoverage.Infrastructure.Tests.Support;

namespace CodeCoverage.Infrastructure.Tests.Persistence.Organizations;

// The proof ADR-013 waits for: writes are stamped and guarded on both engines, unique index included.
[Collection(DatabaseCollection.Name)]
public sealed class OrganizationWriteGuardTests(DatabaseFixture database)
{
  #region Fields

  private const string Key = "Notifications:StatusContextPrefix";

  private const string FirstValue = "first";

  private const string SecondValue = "second";

  private const string NewValue = "changed";

  private const string ForeignRow = "another organization";

  private const string NoOrganization = "has not entered";

  private static readonly DateTimeOffset Now = new(2026, 11, 9, 8, 0, 0, TimeSpan.Zero);

  #endregion

  #region Properties

  private static CancellationToken Ct => TestContext.Current.CancellationToken;

  #endregion

  #region Methods

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task SaveChanges_Added_ShouldStampCurrentOrganization(DatabaseProvider provider)
  {
    var organizations = await TwoOrganizationDatabase.CreateAsync(database, provider, Ct);

    await using (var first = organizations.ContextOf(organizations.FirstId))
    {
      first.OrganizationSettings.Add(NewRow(Key, FirstValue));
      await first.SaveChangesAsync(Ct);
    }

    await using var reader = organizations.ContextOf(organizations.FirstId);

    (await StoredOwnerAsync(reader)).ShouldBe(organizations.FirstId);
    (await ValueOfAsync(organizations, organizations.SecondId)).ShouldBeNull();
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task SaveChanges_AddedWithoutOrganization_ShouldThrow(DatabaseProvider provider)
  {
    var organizations = await TwoOrganizationDatabase.CreateAsync(database, provider, Ct);
    await using var context = organizations.ContextWithoutOrganization();
    context.OrganizationSettings.Add(NewRow(Key, FirstValue));

    var exception = await Should.ThrowAsync<InvalidOperationException>(() => context.SaveChangesAsync(Ct));

    exception.Message.ShouldContain(NoOrganization);
    (await ValueOfAsync(organizations, organizations.FirstId)).ShouldBeNull();
    (await ValueOfAsync(organizations, organizations.SecondId)).ShouldBeNull();
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task SaveChanges_AddedForOtherOrganization_ShouldThrow(DatabaseProvider provider)
  {
    var organizations = await TwoOrganizationDatabase.CreateAsync(database, provider, Ct);
    await using var first = organizations.ContextOf(organizations.FirstId);
    var row = NewRow(Key, FirstValue);
    first.OrganizationSettings.Add(row);
    first.Entry(row).Property<long>(OrganizationOwnedExtensions.ColumnName).CurrentValue = organizations.SecondId;

    var exception = await Should.ThrowAsync<InvalidOperationException>(() => first.SaveChangesAsync(Ct));

    exception.Message.ShouldContain(ForeignRow);
    (await ValueOfAsync(organizations, organizations.SecondId)).ShouldBeNull();
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task SaveChanges_ModifiedRowOfOtherOrganization_ShouldThrow(DatabaseProvider provider)
  {
    var organizations = await SeededAsync(provider);
    var id = await RowIdAsync(organizations, organizations.SecondId);
    await using var first = organizations.ContextOf(organizations.FirstId);
    Track(first, id, organizations.SecondId, EntityState.Modified);

    var exception = await Should.ThrowAsync<InvalidOperationException>(() => first.SaveChangesAsync(Ct));

    exception.Message.ShouldContain(ForeignRow);
    (await ValueOfAsync(organizations, organizations.SecondId)).ShouldBe(SecondValue);
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task SaveChanges_DeletedRowOfOtherOrganization_ShouldThrow(DatabaseProvider provider)
  {
    var organizations = await SeededAsync(provider);
    var id = await RowIdAsync(organizations, organizations.SecondId);
    await using var first = organizations.ContextOf(organizations.FirstId);
    Track(first, id, organizations.SecondId, EntityState.Deleted);

    var exception = await Should.ThrowAsync<InvalidOperationException>(() => first.SaveChangesAsync(Ct));

    exception.Message.ShouldContain(ForeignRow);
    (await ValueOfAsync(organizations, organizations.SecondId)).ShouldBe(SecondValue);
  }

  // The shadow column does not travel with an instance: an attached row holds no organization at all.
  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task SaveChanges_UpdatedDetachedRow_ShouldThrow(DatabaseProvider provider)
  {
    var organizations = await SeededAsync(provider);
    var id = await RowIdAsync(organizations, organizations.SecondId);
    await using var first = organizations.ContextOf(organizations.FirstId);
    first.OrganizationSettings.Update(ExistingRow(id));

    var exception = await Should.ThrowAsync<InvalidOperationException>(() => first.SaveChangesAsync(Ct));

    exception.Message.ShouldContain(ForeignRow);
    (await ValueOfAsync(organizations, organizations.SecondId)).ShouldBe(SecondValue);
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task SaveChanges_RemovedDetachedRow_ShouldThrow(DatabaseProvider provider)
  {
    var organizations = await SeededAsync(provider);
    var id = await RowIdAsync(organizations, organizations.SecondId);
    await using var first = organizations.ContextOf(organizations.FirstId);
    first.OrganizationSettings.Remove(ExistingRow(id));

    var exception = await Should.ThrowAsync<InvalidOperationException>(() => first.SaveChangesAsync(Ct));

    exception.Message.ShouldContain(ForeignRow);
    (await ValueOfAsync(organizations, organizations.SecondId)).ShouldBe(SecondValue);
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task SaveChanges_RowMovedToOtherOrganization_ShouldThrow(DatabaseProvider provider)
  {
    var organizations = await SeededAsync(provider);
    await using var first = organizations.ContextOf(organizations.FirstId);
    var row = await first.OrganizationSettings.SingleAsync(Ct);
    first.Entry(row).Property<long>(OrganizationOwnedExtensions.ColumnName).CurrentValue = organizations.SecondId;

    var exception = await Should.ThrowAsync<InvalidOperationException>(() => first.SaveChangesAsync(Ct));

    exception.Message.ShouldContain(ForeignRow);
    (await ValueOfAsync(organizations, organizations.FirstId)).ShouldBe(FirstValue);
    (await ValueOfAsync(organizations, organizations.SecondId)).ShouldBe(SecondValue);
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task SaveChanges_ModifiedRowOfCurrentOrganization_ShouldSave(DatabaseProvider provider)
  {
    var organizations = await SeededAsync(provider);

    await using (var first = organizations.ContextOf(organizations.FirstId))
    {
      (await first.OrganizationSettings.SingleAsync(Ct)).Value = NewValue;
      await first.SaveChangesAsync(Ct);
    }

    (await ValueOfAsync(organizations, organizations.FirstId)).ShouldBe(NewValue);
    (await ValueOfAsync(organizations, organizations.SecondId)).ShouldBe(SecondValue);
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task SaveChanges_DeletedRowOfCurrentOrganization_ShouldDelete(DatabaseProvider provider)
  {
    var organizations = await SeededAsync(provider);

    await using (var first = organizations.ContextOf(organizations.FirstId))
    {
      first.OrganizationSettings.Remove(await first.OrganizationSettings.SingleAsync(Ct));
      await first.SaveChangesAsync(Ct);
    }

    (await ValueOfAsync(organizations, organizations.FirstId)).ShouldBeNull();
    (await ValueOfAsync(organizations, organizations.SecondId)).ShouldBe(SecondValue);
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task SaveChanges_SameKeyInTwoOrganizations_ShouldKeepBothRows(DatabaseProvider provider)
  {
    var organizations = await SeededAsync(provider);

    (await ValueOfAsync(organizations, organizations.FirstId)).ShouldBe(FirstValue);
    (await ValueOfAsync(organizations, organizations.SecondId)).ShouldBe(SecondValue);
  }

  // The stamped value is what the unique index sees: the same key twice in one organization is refused by the database.
  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task SaveChanges_DuplicateKeyInOneOrganization_ShouldBeRefusedByUniqueIndex(DatabaseProvider provider)
  {
    var organizations = await SeededAsync(provider);
    await using var first = organizations.ContextOf(organizations.FirstId);
    first.OrganizationSettings.Add(NewRow(Key, NewValue));

    await Should.ThrowAsync<DbUpdateException>(() => first.SaveChangesAsync(Ct));

    (await ValueOfAsync(organizations, organizations.FirstId)).ShouldBe(FirstValue);
  }

  // A key replaced in one save: the stamped insert must not collide with the row the same save deletes.
  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task SaveChanges_ReplacedKeyInOneSave_ShouldDeleteBeforeInsert(DatabaseProvider provider)
  {
    var organizations = await SeededAsync(provider);

    await using (var first = organizations.ContextOf(organizations.FirstId))
    {
      first.OrganizationSettings.Remove(await first.OrganizationSettings.SingleAsync(Ct));
      first.OrganizationSettings.Add(NewRow(Key, NewValue));
      await first.SaveChangesAsync(Ct);
    }

    (await ValueOfAsync(organizations, organizations.FirstId)).ShouldBe(NewValue);
    (await ValueOfAsync(organizations, organizations.SecondId)).ShouldBe(SecondValue);
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task SaveChanges_Synchronous_ShouldStampAndGuard(DatabaseProvider provider)
  {
    var organizations = await SeededAsync(provider);
    var id = await RowIdAsync(organizations, organizations.SecondId);

    await using (var first = organizations.ContextOf(organizations.FirstId))
    {
      first.OrganizationSettings.Add(NewRow("Notifications:CommentsEnabled", NewValue));
#pragma warning disable CA1849 // The synchronous overload is the subject of this test.
      first.SaveChanges();
#pragma warning restore CA1849
    }

    await using var attacker = organizations.ContextOf(organizations.FirstId);
    Track(attacker, id, organizations.SecondId, EntityState.Modified);

#pragma warning disable CA1849 // The synchronous overload is the subject of this test.
    Should.Throw<InvalidOperationException>(() => attacker.SaveChanges()).Message.ShouldContain(ForeignRow);
#pragma warning restore CA1849

    await using var reader = organizations.ContextOf(organizations.FirstId);
    (await reader.OrganizationSettings.CountAsync(Ct)).ShouldBe(2);
    (await ValueOfAsync(organizations, organizations.SecondId)).ShouldBe(SecondValue);
  }

  private static StoredOrganizationSetting NewRow(string key, string value) =>
    new() { Key = key, Value = value, UpdatedAt = Now, UpdatedBy = "test" };

  private static StoredOrganizationSetting ExistingRow(long id) =>
    new() { Id = id, Key = Key, Value = NewValue, UpdatedAt = Now, UpdatedBy = "test" };

  // Tracks a row whose original and current organization are the given one, as if it had been read under it.
  private static void Track(CodeCoverageDbContext context, long id, long ownerId, EntityState state)
  {
    var entry = context.Entry(ExistingRow(id));
    entry.Property<long>(OrganizationOwnedExtensions.ColumnName).CurrentValue = ownerId;
    entry.State = state;
    entry.Property<long>(OrganizationOwnedExtensions.ColumnName).OriginalValue.ShouldBe(ownerId);
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

  private static Task<long> StoredOwnerAsync(CodeCoverageDbContext context) =>
    context.OrganizationSettings
           .Select(static s => EF.Property<long>(s, OrganizationOwnedExtensions.ColumnName))
           .SingleAsync(Ct);

  private static async Task<string?> ValueOfAsync(TwoOrganizationDatabase organizations, long organizationId)
  {
    await using var context = organizations.ContextOf(organizationId);

    return await context.OrganizationSettings.Where(static s => s.Key == Key).Select(static s => s.Value).SingleOrDefaultAsync(Ct);
  }

  #endregion
}
