// ------------------------------------------------------------------------------------------------
// <copyright file="SystemClockTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Infrastructure.Time;

namespace CodeCoverage.Infrastructure.Tests.Time;

public sealed class SystemClockTests
{
  #region Methods

  [Fact]
  public void UtcNow_ShouldReadTheTimeProvider()
  {
    var now = new DateTimeOffset(2026, 11, 8, 10, 30, 0, TimeSpan.Zero);
    var timeProvider = A.Fake<TimeProvider>();
    A.CallTo(() => timeProvider.GetUtcNow()).Returns(now);

    new SystemClock(timeProvider).UtcNow.ShouldBe(now);
  }

  [Fact]
  public void UtcNow_WithSystemTime_ShouldBeUtc()
  {
    new SystemClock(TimeProvider.System).UtcNow.Offset.ShouldBe(TimeSpan.Zero);
  }

  #endregion
}
