// ------------------------------------------------------------------------------------------------
// <copyright file="CompetingSaveChangesInterceptor.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.EntityFrameworkCore.Diagnostics;

namespace CodeCoverage.Infrastructure.Tests.Support;

// Lets another writer save just before the first save of the context, and, when that save fails, runs a step after the
// failure and before the caller handles it: a race between a read and a write, ordered exactly, on one thread.
public sealed class CompetingSaveChangesInterceptor(
  Func<CancellationToken, Task> beforeFirstSave,
  Func<CancellationToken, Task>? afterFirstFailure = null) : SaveChangesInterceptor
{
  #region Fields

  private int _saved;

  private int _failed;

  #endregion

  #region Properties

  public bool Failed => Volatile.Read(ref _failed) == 1;

  #endregion

  #region Methods

  public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
    DbContextEventData eventData,
    InterceptionResult<int> result,
    CancellationToken cancellationToken = default)
  {
    if (Interlocked.Exchange(ref _saved, 1) == 0)
    {
      await beforeFirstSave(cancellationToken);
    }

    return result;
  }

  public override async Task SaveChangesFailedAsync(DbContextErrorEventData eventData, CancellationToken cancellationToken = default)
  {
    if (Interlocked.Exchange(ref _failed, 1) == 0 && afterFirstFailure is not null)
    {
      await afterFirstFailure(cancellationToken);
    }
  }

  #endregion
}
