// ------------------------------------------------------------------------------------------------
// <copyright file="IsolationFixture.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Api.Tests.Isolation;

// The database and the seeded world of the isolation suite, shared by its test classes: one container, one host, two
// organizations. Everything is built on first use, so a run without Docker skips instead of failing.
public sealed class IsolationFixture : IAsyncLifetime
{
  #region Fields

  private readonly IsolationDatabase _database = new();

  private readonly SemaphoreSlim _gate = new(1, 1);

  private TwoOrganizationApi? _api;

  #endregion

  #region Methods

  // The seeded world, built on first use.
  public async Task<TwoOrganizationApi> ApiAsync()
  {
    var cancellationToken = TestContext.Current.CancellationToken;

    // The container is started first: without Docker this skips, before anything else is built.
    await _database.ConnectionStringAsync(cancellationToken);
    await _gate.WaitAsync(cancellationToken);

    try
    {
      if (_api is null)
      {
        var api = new TwoOrganizationApi(_database);
        await api.InitializeAsync();
        _api = api;
      }

      return _api;
    }
    finally
    {
      _gate.Release();
    }
  }

  /// <inheritdoc />
  public async ValueTask InitializeAsync() => await _database.InitializeAsync();

  /// <inheritdoc />
  public async ValueTask DisposeAsync()
  {
    if (_api is not null)
    {
      await _api.DisposeAsync();
    }

    await _database.DisposeAsync();
    _gate.Dispose();
  }

  #endregion
}
