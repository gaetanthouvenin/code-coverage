// ------------------------------------------------------------------------------------------------
// <copyright file="CapturingSaveChangesInterceptor.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace CodeCoverage.Infrastructure.Tests.Support;

// Captures the entities a context is about to insert and suppresses the save, so that a test can
// check what would be written without a database server.
public sealed class CapturingSaveChangesInterceptor : SaveChangesInterceptor
{
  #region Fields

  private readonly List<object> _added = [];

  #endregion

  #region Properties

  public IReadOnlyList<object> Added => _added;

  public int SaveCount { get; private set; }

  #endregion

  #region Methods

  public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
    DbContextEventData eventData,
    InterceptionResult<int> result,
    CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(eventData);

    var added = eventData.Context!.ChangeTracker.Entries()
                         .Where(static e => e.State == EntityState.Added)
                         .Select(static e => e.Entity)
                         .ToList();

    _added.AddRange(added);
    SaveCount++;

    return ValueTask.FromResult(InterceptionResult<int>.SuppressWithResult(added.Count));
  }

  #endregion
}
