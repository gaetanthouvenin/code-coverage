// ------------------------------------------------------------------------------------------------
// <copyright file="DesignTimeDbContextFactoryTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

using CodeCoverage.Infrastructure.Persistence;

namespace CodeCoverage.Infrastructure.Tests.Persistence;

public sealed class DesignTimeDbContextFactoryTests
{
  #region Properties

  public static TheoryData<string, string> Factories =>
    new()
    {
      { nameof(PostgreSql), "Npgsql.EntityFrameworkCore.PostgreSQL" },
      { nameof(SqlServer), "Microsoft.EntityFrameworkCore.SqlServer" },
    };

  #endregion

  #region Methods

  [Theory]
  [MemberData(nameof(Factories))]
  public void CreateDbContext_ForEachMigrationsProject_ShouldTargetItsProvider(string project, string providerName)
  {
    using var context = Factory(project).CreateDbContext([]);

    context.Database.ProviderName.ShouldBe(providerName);
    context.Database.GetMigrations().ShouldNotBeEmpty();
  }

  [Fact]
  public void ConnectionVariable_ShouldBeSharedByBothFactories()
  {
    PostgreSql.DesignTimeDbContextFactory.ConnectionVariable.ShouldBe("CODECOVERAGE_DESIGN_CONNECTION");
    SqlServer.DesignTimeDbContextFactory.ConnectionVariable.ShouldBe("CODECOVERAGE_DESIGN_CONNECTION");
  }

  private static IDesignTimeDbContextFactory<CodeCoverageDbContext> Factory(string project) =>
    project switch
    {
      nameof(PostgreSql) => new PostgreSql.DesignTimeDbContextFactory(),
      nameof(SqlServer) => new SqlServer.DesignTimeDbContextFactory(),
      var _ => throw new ArgumentOutOfRangeException(nameof(project), project, "Unknown migrations project."),
    };

  #endregion
}
