// ------------------------------------------------------------------------------------------------
// <copyright file="IsolationFixture.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Web.Tests.Isolation;

// The database and the seeded world of the isolation suite, shared by its test classes: one container, one host, two
// organizations. Everything is built on first use, so a run without Docker skips instead of failing.
public sealed class IsolationFixture : IAsyncLifetime
{
  #region Fields

  private readonly IsolationDatabase _database = new();

  private readonly SemaphoreSlim _gate = new(1, 1);

  private TwoOrganizationWeb? _web;

  #endregion

  #region Methods

  // The seeded world, built on first use.
  public async Task<TwoOrganizationWeb> WebAsync()
  {
    var cancellationToken = TestContext.Current.CancellationToken;

    // The container is started first: without Docker this skips, before anything else is built.
    await _database.ConnectionStringAsync(cancellationToken);
    await _gate.WaitAsync(cancellationToken);

    try
    {
      if (_web is null)
      {
        var web = new TwoOrganizationWeb(_database);
        await web.InitializeAsync();
        _web = web;
      }

      return _web;
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
    if (_web is not null)
    {
      await _web.DisposeAsync();
    }

    await _database.DisposeAsync();
    _gate.Dispose();
  }

  #endregion
}
