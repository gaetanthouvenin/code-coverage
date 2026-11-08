// ------------------------------------------------------------------------------------------------
// <copyright file="KeyBlindJobClaimDialect.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Data.Common;
using System.Runtime.CompilerServices;

using CodeCoverage.Infrastructure.Jobs;
using CodeCoverage.Infrastructure.Persistence;

namespace CodeCoverage.Infrastructure.Tests.Support;

// The dialect of an engine without its NOT EXISTS clause on the running jobs of the key: it claims a
// job whose key is already running, as a concurrent claim would, so that the Running index refuses it.
internal sealed class KeyBlindJobClaimDialect(IJobClaimDialect inner) : IJobClaimDialect
{
  #region Implementation of IJobClaimDialect

  public DatabaseProvider Provider => inner.Provider;

  public FormattableString ClaimNext(string lockedBy, DateTimeOffset now, DateTimeOffset lockedUntil)
  {
    var statement = inner.ClaimNext(lockedBy, now, lockedUntil);
    var format = statement.Format;
    var start = format.IndexOf("OR NOT EXISTS", StringComparison.Ordinal);
    var end = format.IndexOf("))", start, StringComparison.Ordinal) + 1;

    // "(LockKey IS NULL OR NOT EXISTS (...))" becomes "(LockKey IS NULL OR 1 = 1)".
    return FormattableStringFactory.Create(
      string.Concat(format.AsSpan(0, start), "OR 1 = 1", format.AsSpan(end)),
      statement.GetArguments()
    );
  }

  public bool IsDeadlock(DbException exception) => inner.IsDeadlock(exception);

  #endregion
}
