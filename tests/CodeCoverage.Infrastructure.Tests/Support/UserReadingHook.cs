// ------------------------------------------------------------------------------------------------
// <copyright file="UserReadingHook.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Infrastructure.Tests.Support;

// Counts the readings of the row of a user, shared by every scope, and runs an action before the reading of a rank,
// and another one before its first save.
internal sealed class UserReadingHook(int rank, Func<Task> action, Func<Task>? beforeFirstSave = null)
{
  #region Fields

  private int _readings;

  private int _saves;

  #endregion

  #region Properties

  public int Readings => Volatile.Read(ref _readings);

  #endregion

  #region Methods

  public async Task BeforeReadingAsync()
  {
    if (Interlocked.Increment(ref _readings) == rank)
    {
      await action().ConfigureAwait(false);
    }
  }

  public async Task BeforeSaveAsync()
  {
    if (Interlocked.Increment(ref _saves) == 1 && beforeFirstSave is not null)
    {
      await beforeFirstSave().ConfigureAwait(false);
    }
  }

  // Waits until the row was read a number of times, by whoever reads it.
  public async Task ReachedAsync(int readings, CancellationToken cancellationToken)
  {
    using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
    deadline.CancelAfter(TimeSpan.FromSeconds(30));

    while (Readings < readings)
    {
      await Task.Delay(5, deadline.Token).ConfigureAwait(false);
    }
  }

  #endregion
}
