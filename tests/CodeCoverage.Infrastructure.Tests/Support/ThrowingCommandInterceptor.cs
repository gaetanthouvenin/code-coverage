// ------------------------------------------------------------------------------------------------
// <copyright file="ThrowingCommandInterceptor.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Data.Common;

using Microsoft.EntityFrameworkCore.Diagnostics;

namespace CodeCoverage.Infrastructure.Tests.Support;

// Throws, once, instead of running the first command whose text matches.
internal sealed class ThrowingCommandInterceptor(Func<string, bool> matches, Func<Exception> failure) : DbCommandInterceptor
{
  #region Fields

  private int _thrown;

  #endregion

  #region Methods

  public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
    DbCommand command,
    CommandEventData eventData,
    InterceptionResult<DbDataReader> result,
    CancellationToken cancellationToken = default) =>
    matches(command.CommandText) && Interlocked.Exchange(ref _thrown, 1) == 0 ? throw failure() : ValueTask.FromResult(result);

  public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
    DbCommand command,
    CommandEventData eventData,
    InterceptionResult<int> result,
    CancellationToken cancellationToken = default) =>
    matches(command.CommandText) && Interlocked.Exchange(ref _thrown, 1) == 0 ? throw failure() : ValueTask.FromResult(result);

  #endregion
}
