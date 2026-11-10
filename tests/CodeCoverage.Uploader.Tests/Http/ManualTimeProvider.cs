// ------------------------------------------------------------------------------------------------
// <copyright file="ManualTimeProvider.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Uploader.Tests.Http;

// A clock that only moves when a test moves it: the waits of the uploader cost no real time.
public sealed class ManualTimeProvider : TimeProvider
{
  #region Fields

  private DateTimeOffset _now = new(2026, 11, 10, 8, 0, 0, TimeSpan.Zero);

  #endregion

  #region Methods

  public override DateTimeOffset GetUtcNow() => _now;

  public void Advance(TimeSpan by) => _now += by;

  #endregion
}
