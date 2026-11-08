// ------------------------------------------------------------------------------------------------
// <copyright file="DesignTimeDbContextFactory.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

using CodeCoverage.Infrastructure.Persistence;

namespace CodeCoverage.Infrastructure.SqlServer;

/// <summary>
///   Builds the context for <c>dotnet ef</c>, on SQL Server, so that the migrations of this engine are
///   generated into this assembly.
/// </summary>
/// <remarks>
///   <code>dotnet ef migrations add &lt;Name&gt; --project src/CodeCoverage.Infrastructure.SqlServer --startup-project src/CodeCoverage.Infrastructure.SqlServer</code>
///   The connection string is only used by the commands that reach the database
///   (<c>database update</c>); override it with the <c>CODECOVERAGE_DESIGN_CONNECTION</c> environment
///   variable.
/// </remarks>
internal sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<CodeCoverageDbContext>
{
  #region Fields

  /// <summary>The environment variable that overrides the connection string.</summary>
  public const string ConnectionVariable = "CODECOVERAGE_DESIGN_CONNECTION";

  private const string DefaultConnection =
    "Server=localhost;Database=CodeCoverage_Design;Trusted_Connection=True;TrustServerCertificate=True";

  #endregion

  #region Implementation of IDesignTimeDbContextFactory<CodeCoverageDbContext>

  /// <inheritdoc />
  public CodeCoverageDbContext CreateDbContext(string[] args)
  {
    var connectionString = Environment.GetEnvironmentVariable(ConnectionVariable) is { Length: > 0 } variable
                             ? variable
                             : DefaultConnection;

    var options = new DbContextOptionsBuilder<CodeCoverageDbContext>();
    options.UseCodeCoverageDatabase(DatabaseProvider.SqlServer, connectionString);

    return new CodeCoverageDbContext(options.Options);
  }

  #endregion
}
