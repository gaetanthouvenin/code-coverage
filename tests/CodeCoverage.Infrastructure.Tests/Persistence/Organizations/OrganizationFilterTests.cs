// ------------------------------------------------------------------------------------------------
// <copyright file="OrganizationFilterTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.EntityFrameworkCore;

using CodeCoverage.Infrastructure.Persistence;
using CodeCoverage.Infrastructure.Tests.Support;

namespace CodeCoverage.Infrastructure.Tests.Persistence.Organizations;

// The proof ADR-013 waits for: the filter holds on queries and on bulk statements, on both engines.
[Collection(DatabaseCollection.Name)]
public sealed class OrganizationFilterTests(DatabaseFixture database)
{
  #region Fields

  private const string Key = "Notifications:StatusContextPrefix";

  private const string FirstValue = "first";

  private const string SecondValue = "second";

  private const string NewValue = "changed";

  #endregion

  #region Properties

  private static CancellationToken Ct => TestContext.Current.CancellationToken;

  #endregion

  #region Methods

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task Query_WithoutOrganization_ShouldThrow(DatabaseProvider provider)
  {
    var organizations = await SeededAsync(provider);
    await using var context = organizations.ContextWithoutOrganization();

    await Should.ThrowAsync<InvalidOperationException>(() => context.OrganizationSettings.ToListAsync(Ct));
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task Query_OtherOrganizationRows_ShouldReturnNothing(DatabaseProvider provider)
  {
    var organizations = await TwoOrganizationDatabase.CreateAsync(database, provider, Ct);
    await organizations.AddSettingAsync(organizations.SecondId, Key, SecondValue, Ct);

    await using var first = organizations.ContextOf(organizations.FirstId);
    await using var second = organizations.ContextOf(organizations.SecondId);

    (await first.OrganizationSettings.ToListAsync(Ct)).ShouldBeEmpty();
    (await second.OrganizationSettings.SingleAsync(Ct)).Value.ShouldBe(SecondValue);
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task ExecuteUpdate_OtherOrganizationRows_ShouldAffectNone(DatabaseProvider provider)
  {
    var organizations = await SeededAsync(provider);
    await using var first = organizations.ContextOf(organizations.FirstId);

    var affected = await first.OrganizationSettings
                              .Where(static s => s.Key == Key)
                              .ExecuteUpdateAsync(static s => s.SetProperty(static r => r.Value, NewValue), Ct);

    affected.ShouldBe(1);
    (await ValueOfAsync(organizations, organizations.FirstId)).ShouldBe(NewValue);
    (await ValueOfAsync(organizations, organizations.SecondId)).ShouldBe(SecondValue);
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task ExecuteUpdate_WithoutOrganization_ShouldThrow(DatabaseProvider provider)
  {
    var organizations = await SeededAsync(provider);
    await using var context = organizations.ContextWithoutOrganization();

    await Should.ThrowAsync<InvalidOperationException>(
      () => context.OrganizationSettings.ExecuteUpdateAsync(static s => s.SetProperty(static r => r.Value, NewValue), Ct)
    );

    (await ValueOfAsync(organizations, organizations.FirstId)).ShouldBe(FirstValue);
    (await ValueOfAsync(organizations, organizations.SecondId)).ShouldBe(SecondValue);
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task ExecuteDelete_OtherOrganizationRows_ShouldAffectNone(DatabaseProvider provider)
  {
    var organizations = await SeededAsync(provider);
    await using var first = organizations.ContextOf(organizations.FirstId);

    var affected = await first.OrganizationSettings.Where(static s => s.Key == Key).ExecuteDeleteAsync(Ct);

    affected.ShouldBe(1);
    (await ValueOfAsync(organizations, organizations.FirstId)).ShouldBeNull();
    (await ValueOfAsync(organizations, organizations.SecondId)).ShouldBe(SecondValue);
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task ExecuteDelete_WithoutOrganization_ShouldThrow(DatabaseProvider provider)
  {
    var organizations = await SeededAsync(provider);
    await using var context = organizations.ContextWithoutOrganization();

    await Should.ThrowAsync<InvalidOperationException>(() => context.OrganizationSettings.ExecuteDeleteAsync(Ct));

    (await ValueOfAsync(organizations, organizations.FirstId)).ShouldBe(FirstValue);
    (await ValueOfAsync(organizations, organizations.SecondId)).ShouldBe(SecondValue);
  }

  // The model is built once per process with the first context: each context must still read its own organization.
  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task TwoContexts_InOneProcess_ShouldUseTheirOwnOrganization(DatabaseProvider provider)
  {
    var organizations = await SeededAsync(provider);
    await using var first = organizations.ContextOf(organizations.FirstId);
    await using var second = organizations.ContextOf(organizations.SecondId);

    (await first.OrganizationSettings.SingleAsync(Ct)).Value.ShouldBe(FirstValue);
    (await second.OrganizationSettings.SingleAsync(Ct)).Value.ShouldBe(SecondValue);
    (await first.OrganizationSettings.CountAsync(Ct)).ShouldBe(1);
  }

  private async Task<TwoOrganizationDatabase> SeededAsync(DatabaseProvider provider)
  {
    var organizations = await TwoOrganizationDatabase.CreateAsync(database, provider, Ct);
    await organizations.AddSettingAsync(organizations.FirstId, Key, FirstValue, Ct);
    await organizations.AddSettingAsync(organizations.SecondId, Key, SecondValue, Ct);

    return organizations;
  }

  private static async Task<string?> ValueOfAsync(TwoOrganizationDatabase organizations, long organizationId)
  {
    await using var context = organizations.ContextOf(organizationId);

    return await context.OrganizationSettings.Where(static s => s.Key == Key).Select(static s => s.Value).SingleOrDefaultAsync(Ct);
  }

  #endregion
}
