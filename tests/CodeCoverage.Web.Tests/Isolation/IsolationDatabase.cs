// ------------------------------------------------------------------------------------------------
// <copyright file="IsolationDatabase.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using DotNet.Testcontainers.Builders;

using Testcontainers.PostgreSql;

namespace CodeCoverage.Web.Tests.Isolation;

// One PostgreSQL 17 container for the isolation suite, started on first use, holding the one database the host
// migrates. Without Docker, the tests are skipped (with the reason) instead of failing the run.
public sealed class IsolationDatabase : IAsyncLifetime
{
  #region Fields

  private readonly SemaphoreSlim _gate = new(1, 1);

  private PostgreSqlContainer? _container;

  private string? _skipReason;

  #endregion

  #region Methods

  // The connection string of the server, started on the first call.
  public async Task<string> ConnectionStringAsync(CancellationToken cancellationToken)
  {
    if (_skipReason is not null)
    {
      Assert.Skip(_skipReason);
    }

    await _gate.WaitAsync(cancellationToken);

    try
    {
      _container ??= await StartAsync(cancellationToken);

      return _container.GetConnectionString();
    }
    finally
    {
      _gate.Release();
    }
  }

  public async ValueTask InitializeAsync() => await Task.CompletedTask;

  public async ValueTask DisposeAsync()
  {
    if (_container is not null)
    {
      await _container.DisposeAsync();
    }

    _gate.Dispose();
  }

  private async Task<PostgreSqlContainer> StartAsync(CancellationToken cancellationToken)
  {
    var container = new PostgreSqlBuilder("postgres:17-alpine").WithDatabase("codecoverage").Build();

    try
    {
      await container.StartAsync(cancellationToken);

      return container;
    }
    catch (DockerUnavailableException exception)
    {
      await container.DisposeAsync();
      _skipReason = $"Docker is not available, the database container cannot start: {exception.Message}";
      Assert.Skip(_skipReason);

      throw;
    }
  }

  #endregion
}
