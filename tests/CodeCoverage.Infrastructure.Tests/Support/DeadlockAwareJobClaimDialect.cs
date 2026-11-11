// ------------------------------------------------------------------------------------------------
// <copyright file="DeadlockAwareJobClaimDialect.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Data.Common;

using CodeCoverage.Infrastructure.Jobs;
using CodeCoverage.Infrastructure.Persistence;

namespace CodeCoverage.Infrastructure.Tests.Support;

// The dialect of an engine that also takes a TestDeadlockException for a deadlock.
internal sealed class DeadlockAwareJobClaimDialect(IJobClaimDialect inner) : IJobClaimDialect
{
  #region Implementation of IJobClaimDialect

  public DatabaseProvider Provider => inner.Provider;

  public FormattableString ClaimNext(string lockedBy, DateTimeOffset now, DateTimeOffset lockedUntil) => inner.ClaimNext(lockedBy, now, lockedUntil);

  public bool IsDeadlock(DbException exception) => exception is TestDeadlockException || inner.IsDeadlock(exception);

  #endregion
}
