// ------------------------------------------------------------------------------------------------
// <copyright file="DbContextTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

using CodeCoverage.Infrastructure.Persistence;
using CodeCoverage.Infrastructure.Tests.Support;

namespace CodeCoverage.Infrastructure.Tests.Persistence;

[Collection(DatabaseCollection.Name)]
public sealed class DbContextTests(DatabaseFixture database)
{
  #region Fields

  private const string InitialSchema = "20261108004044_InitialSchema";

  // Never opened: building the model and listing the migrations do not reach the server.
  private const string UnusedConnection = "Server=localhost;Database=unused";

  #endregion

  #region Properties

  private static CancellationToken Ct => TestContext.Current.CancellationToken;

  #endregion

  #region Methods

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task Create_ForEachProvider_ShouldConnect(DatabaseProvider provider)
  {
    var connectionString = await database.NewDatabaseAsync(provider, Ct);
    await using var context = DatabaseFixture.CreateContext(provider, connectionString);

    await context.Database.MigrateAsync(Ct);

    (await context.Database.CanConnectAsync(Ct)).ShouldBeTrue();
    var applied = (await context.Database.GetAppliedMigrationsAsync(Ct)).ToList();
    applied.ShouldBe(context.Database.GetMigrations());
    applied[0].ShouldBe(InitialSchema);
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public void GetMigrations_ForEachProvider_ShouldStartWithInitialSchema(DatabaseProvider provider)
  {
    using var context = DatabaseFixture.CreateContext(provider, UnusedConnection);

    context.Database.GetMigrations().First().ShouldBe(InitialSchema);
  }

  [Fact]
  public void Model_OnPostgreSql_ShouldUseIdentityByDefaultColumns()
  {
    using var context = DatabaseFixture.CreateContext(DatabaseProvider.PostgreSql, UnusedConnection);

    NpgsqlModelExtensions.GetValueGenerationStrategy(context.Model).ShouldBe(NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);
  }

  [Fact]
  public void Model_OnSqlServer_ShouldUseIdentityColumns()
  {
    using var context = DatabaseFixture.CreateContext(DatabaseProvider.SqlServer, UnusedConnection);

    SqlServerModelExtensions.GetValueGenerationStrategy(context.Model).ShouldBe(SqlServerValueGenerationStrategy.IdentityColumn);
  }

  #endregion
}
