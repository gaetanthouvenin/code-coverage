// ------------------------------------------------------------------------------------------------
// <copyright file="TwoOrganizationDatabase.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.EntityFrameworkCore;

using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Infrastructure.Identity;
using CodeCoverage.Infrastructure.Persistence;
using CodeCoverage.Infrastructure.Persistence.Repositories;
using CodeCoverage.Infrastructure.Settings;

namespace CodeCoverage.Infrastructure.Tests.Support;

// A migrated database of its own holding two organizations, and the contexts that have entered them.
public sealed class TwoOrganizationDatabase
{
  #region Fields

  private static readonly DateTimeOffset Now = new(2026, 11, 8, 10, 30, 0, TimeSpan.Zero);

  private readonly string _connectionString;

  private readonly DatabaseProvider _provider;

  #endregion

  #region Ctors

  private TwoOrganizationDatabase(DatabaseProvider provider, string connectionString, long firstId, long secondId)
  {
    _provider = provider;
    _connectionString = connectionString;
    FirstId = firstId;
    SecondId = secondId;
  }

  #endregion

  #region Properties

  public long FirstId { get; }

  public long SecondId { get; }

  #endregion

  #region Methods

  public static async Task<TwoOrganizationDatabase> CreateAsync(
    DatabaseFixture database,
    DatabaseProvider provider,
    CancellationToken cancellationToken)
  {
    var connectionString = await database.NewDatabaseAsync(provider, cancellationToken);
    await using var context = DatabaseFixture.CreateContext(provider, connectionString);
    await context.Database.MigrateAsync(cancellationToken);

    var clock = A.Fake<IClock>();
    A.CallTo(() => clock.UtcNow).Returns(Now);
    var directory = new OrganizationDirectory(context, clock, new OrganizationContext());

    var first = await directory.UpsertFromForgeAsync(Forge.GitHub, "1001", "first-org", OrganizationAccountType.Organization, cancellationToken);
    var second = await directory.UpsertFromForgeAsync(Forge.GitHub, "2002", "second-org", OrganizationAccountType.Organization, cancellationToken);

    return new TwoOrganizationDatabase(provider, connectionString, first.Id, second.Id);
  }

  public CodeCoverageDbContext ContextWithoutOrganization() => DatabaseFixture.CreateContext(_provider, _connectionString);

  public CodeCoverageDbContext ContextOf(long organizationId)
  {
    var organization = new OrganizationContext();
    organization.Enter(organizationId);

    return DatabaseFixture.CreateContext(_provider, _connectionString, organization);
  }

  public async Task AddSettingAsync(long organizationId, string key, string value, CancellationToken cancellationToken)
  {
    await using var context = ContextOf(organizationId);

    context.OrganizationSettings.Add(new StoredOrganizationSetting { Key = key, Value = value, UpdatedAt = Now, UpdatedBy = "test" });

    await context.SaveChangesAsync(cancellationToken);
  }

  #endregion
}
