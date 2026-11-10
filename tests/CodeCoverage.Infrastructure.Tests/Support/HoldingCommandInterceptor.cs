// ------------------------------------------------------------------------------------------------
// <copyright file="HoldingCommandInterceptor.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Data.Common;

using Microsoft.EntityFrameworkCore.Diagnostics;

namespace CodeCoverage.Infrastructure.Tests.Support;

// Holds the first command that matches, before it runs, until the test opens the gate: the transaction of the context
// keeps the locks it already took, so that a test can order two transactions exactly.
public sealed class HoldingCommandInterceptor(Func<string, bool> matches) : DbCommandInterceptor
{
  #region Fields

  private int _held;

  #endregion

  #region Properties

  public TaskCompletionSource Reached { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

  public TaskCompletionSource Gate { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

  #endregion

  #region Methods

  public override async ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
    DbCommand command,
    CommandEventData eventData,
    InterceptionResult<int> result,
    CancellationToken cancellationToken = default)
  {
    if (matches(command.CommandText) && Interlocked.Exchange(ref _held, 1) == 0)
    {
      Reached.TrySetResult();
      await Gate.Task.WaitAsync(cancellationToken);
    }

    return result;
  }

  #endregion
}
