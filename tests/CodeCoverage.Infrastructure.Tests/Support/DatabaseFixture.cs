// ------------------------------------------------------------------------------------------------
// <copyright file="DatabaseFixture.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;

using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

using Npgsql;

using Testcontainers.MsSql;
using Testcontainers.PostgreSql;

using CodeCoverage.Infrastructure.Persistence;

namespace CodeCoverage.Infrastructure.Tests.Support;

// One PostgreSQL 17 and one SQL Server 2022 container for the whole collection, each started on
// first use; every test gets its own empty database on them. Without Docker, the tests that need a
// server are skipped (with the reason) instead of failing the run.
public sealed class DatabaseFixture : IAsyncLifetime
{
  #region Fields

  private const string PostgreSqlImage = "postgres:17-alpine";

  private const string SqlServerImage = "mcr.microsoft.com/mssql/server:2022-latest";

  private readonly Dictionary<DatabaseProvider, DockerContainer> _containers = [];

  private readonly SemaphoreSlim _gate = new(1, 1);

  private string? _skipReason;

  #endregion

  #region Methods

  public static CodeCoverageDbContext CreateContext(DatabaseProvider provider, string connectionString)
  {
    var options = new DbContextOptionsBuilder<CodeCoverageDbContext>();
    options.UseCodeCoverageDatabase(provider, connectionString);

    return new CodeCoverageDbContext(options.Options);
  }

  // A connection string to a database that does not exist yet: the migration creates it.
  public async Task<string> NewDatabaseAsync(DatabaseProvider provider, CancellationToken cancellationToken)
  {
    var server = await ServerAsync(provider, cancellationToken);
    var name = $"cc_{Guid.NewGuid():N}";

    return provider switch
    {
      DatabaseProvider.PostgreSql => new NpgsqlConnectionStringBuilder(server) { Database = name }.ConnectionString,
      DatabaseProvider.SqlServer => new SqlConnectionStringBuilder(server) { InitialCatalog = name }.ConnectionString,
      var _ => throw new ArgumentOutOfRangeException(nameof(provider), provider, "Unknown database provider."),
    };
  }

  private static DockerContainer Build(DatabaseProvider provider) =>
    provider switch
    {
      // Each test has its own database, hence its own connection pool: their idle connections add up.
      DatabaseProvider.PostgreSql => new PostgreSqlBuilder(PostgreSqlImage).WithCommand("-c", "max_connections=500").Build(),
      DatabaseProvider.SqlServer => new MsSqlBuilder(SqlServerImage).Build(),
      var _ => throw new ArgumentOutOfRangeException(nameof(provider), provider, "Unknown database provider."),
    };

  // Builds and starts the container; whatever the failure, the container is disposed so no
  // Testcontainers resource leaks. Docker missing is remembered so later tests skip immediately.
  private async Task<DockerContainer> StartAsync(DatabaseProvider provider, CancellationToken cancellationToken)
  {
    DockerContainer? container = null;

    try
    {
      container = Build(provider);
      await container.StartAsync(cancellationToken);

      return container;
    }
    catch (DockerUnavailableException exception)
    {
      await DisposeQuietlyAsync(container);
      _skipReason = $"Docker is not available, the database containers cannot start: {exception.Message}";
      Assert.Skip(_skipReason);
      throw;
    }
    catch
    {
      await DisposeQuietlyAsync(container);
      throw;
    }
  }

  private static async Task DisposeQuietlyAsync(DockerContainer? container)
  {
    if (container is null)
    {
      return;
    }

    try
    {
      await container.DisposeAsync();
    }
    catch (Exception)
    {
      // The start failure is the error worth reporting; a failed clean-up must not hide it.
    }
  }

  private async Task<string> ServerAsync(DatabaseProvider provider, CancellationToken cancellationToken)
  {
    await _gate.WaitAsync(cancellationToken);

    try
    {
      if (_skipReason is not null)
      {
        Assert.Skip(_skipReason);
      }

      if (!_containers.TryGetValue(provider, out var container))
      {
        container = await StartAsync(provider, cancellationToken);
        _containers[provider] = container;
      }

      return ((IDatabaseContainer)container).GetConnectionString();
    }
    finally
    {
      _gate.Release();
    }
  }

  #endregion

  #region Implementation of IAsyncLifetime

  public ValueTask InitializeAsync() => ValueTask.CompletedTask;

  public async ValueTask DisposeAsync()
  {
    foreach (var container in _containers.Values)
    {
      await container.DisposeAsync();
    }

    _containers.Clear();
    _gate.Dispose();
  }

  #endregion
}
