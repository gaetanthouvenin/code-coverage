// ------------------------------------------------------------------------------------------------
// <copyright file="FakeSystemClock.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.Extensions.Internal;

namespace CodeCoverage.Infrastructure.Tests.Support;

// The clock of a MemoryCache, driven by the time provider of a test: an entry expires when the test says so.
public sealed class FakeSystemClock(TimeProvider time) : ISystemClock
{
  #region Implementation of ISystemClock

  public DateTimeOffset UtcNow => time.GetUtcNow();

  #endregion
}