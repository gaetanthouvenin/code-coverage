// ------------------------------------------------------------------------------------------------
// <copyright file="FailingSaveChangesInterceptor.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace CodeCoverage.Infrastructure.Tests.Support;

// Fails every save before the connection is needed, as a database error would, so that a test can
// check how a component recovers without a database server.
public sealed class FailingSaveChangesInterceptor : SaveChangesInterceptor
{
  #region Methods

  public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
    DbContextEventData eventData,
    InterceptionResult<int> result,
    CancellationToken cancellationToken = default) =>
    throw new DbUpdateException("Simulated database failure.");

  #endregion
}
