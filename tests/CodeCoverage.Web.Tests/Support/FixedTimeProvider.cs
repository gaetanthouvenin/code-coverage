// ------------------------------------------------------------------------------------------------
// <copyright file="FixedTimeProvider.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Web.Tests.Support;

// A clock stopped at one instant, for the pages that compute a range from now.
public sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
  #region Methods

  public override DateTimeOffset GetUtcNow() => now;

  #endregion
}
