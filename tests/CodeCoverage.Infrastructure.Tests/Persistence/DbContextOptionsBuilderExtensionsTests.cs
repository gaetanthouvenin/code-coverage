// ------------------------------------------------------------------------------------------------
// <copyright file="DbContextOptionsBuilderExtensionsTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

using CodeCoverage.Infrastructure.Persistence;

namespace CodeCoverage.Infrastructure.Tests.Persistence;

public sealed class DbContextOptionsBuilderExtensionsTests
{
  #region Fields

  private const string Connection = "Server=localhost;Database=unused";

  #endregion

  #region Methods

  [Theory]
  [InlineData(DatabaseProvider.PostgreSql, "Npgsql.EntityFrameworkCore.PostgreSQL", DbContextOptionsBuilderExtensions.PostgreSqlMigrationsAssembly)]
  [InlineData(DatabaseProvider.SqlServer, "Microsoft.EntityFrameworkCore.SqlServer", DbContextOptionsBuilderExtensions.SqlServerMigrationsAssembly)]
  public void UseCodeCoverageDatabase_ForEachProvider_ShouldUseItsMigrationsAssembly(
    DatabaseProvider provider,
    string providerName,
    string migrationsAssembly)
  {
    var options = new DbContextOptionsBuilder<CodeCoverageDbContext>();

    options.UseCodeCoverageDatabase(provider, Connection).ShouldBeSameAs(options);

    using var context = new CodeCoverageDbContext(options.Options);
    context.Database.ProviderName.ShouldBe(providerName);
    RelationalOptionsExtension.Extract(options.Options).MigrationsAssembly.ShouldBe(migrationsAssembly);
  }

  [Fact]
  public void MigrationsAssemblies_ShouldNameTheMigrationsProjects()
  {
    DbContextOptionsBuilderExtensions.PostgreSqlMigrationsAssembly
                                     .ShouldBe(typeof(PostgreSql.DesignTimeDbContextFactory).Assembly.GetName().Name);
    DbContextOptionsBuilderExtensions.SqlServerMigrationsAssembly
                                     .ShouldBe(typeof(SqlServer.DesignTimeDbContextFactory).Assembly.GetName().Name);
  }

  [Fact]
  public void UseCodeCoverageDatabase_WithUnknownProvider_ShouldThrowArgumentOutOfRangeException()
  {
    var options = new DbContextOptionsBuilder<CodeCoverageDbContext>();

    Should.Throw<ArgumentOutOfRangeException>(() => options.UseCodeCoverageDatabase((DatabaseProvider)42, Connection))
          .ParamName.ShouldBe("provider");
  }

  [Theory]
  [InlineData("")]
  [InlineData(" ")]
  public void UseCodeCoverageDatabase_WithBlankConnectionString_ShouldThrowArgumentException(string connectionString)
  {
    var options = new DbContextOptionsBuilder<CodeCoverageDbContext>();

    Should.Throw<ArgumentException>(() => options.UseCodeCoverageDatabase(DatabaseProvider.PostgreSql, connectionString));
  }

  #endregion
}
