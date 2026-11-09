// ------------------------------------------------------------------------------------------------
// <copyright file="Place.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Application.Services;

/// <summary>A place taken in a <see cref="ParseGate" />, given back once.</summary>
/// <param name="semaphore">The semaphore the place was taken from.</param>
internal sealed class Place(SemaphoreSlim semaphore) : IDisposable
{
  #region Fields

  private int _released;

  #endregion

  #region Implementation of IDisposable

  /// <inheritdoc />
  public void Dispose()
  {
    if (Interlocked.Exchange(ref _released, 1) == 0)
    {
      semaphore.Release();
    }
  }

  #endregion
}
