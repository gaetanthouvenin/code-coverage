// ------------------------------------------------------------------------------------------------
// <copyright file="ParseGate.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.Extensions.Options;

using CodeCoverage.Application.Configuration;

namespace CodeCoverage.Application.Services;

/// <summary>Limits the number of reports parsed at the same time in the process.</summary>
/// <param name="options">The processing settings, read at each entry.</param>
internal sealed class ParseGate(IOptionsMonitor<ProcessingOptions> options)
{
  #region Fields

  private readonly object _sync = new();

  private SemaphoreSlim? _semaphore;

  private int _limit;

  #endregion

  #region Methods

  /// <summary>Waits for a place.</summary>
  /// <param name="cancellationToken">A token to cancel the wait.</param>
  /// <returns>What to dispose of to give the place back.</returns>
  public async Task<IDisposable> EnterAsync(CancellationToken cancellationToken)
  {
    var semaphore = Current();

    await semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);

    return new Place(semaphore);
  }

  /// <summary>Gets the semaphore of the current setting: a new value applies to the next entries.</summary>
  /// <returns>The semaphore.</returns>
  private SemaphoreSlim Current()
  {
    var limit = Math.Max(1, options.CurrentValue.MaxParallelParses);

    lock (_sync)
    {
      if (_semaphore is null || _limit != limit)
      {
        _semaphore = new SemaphoreSlim(limit, limit);
        _limit = limit;
      }

      return _semaphore;
    }
  }

  #endregion
}
