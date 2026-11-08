// ------------------------------------------------------------------------------------------------
// <copyright file="JobRetryPolicyTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Infrastructure.Jobs;

namespace CodeCoverage.Infrastructure.Tests.Jobs;

public sealed class JobRetryPolicyTests
{
  #region Methods

  [Theory]
  [InlineData(0, 10)]
  [InlineData(1, 20)]
  [InlineData(2, 40)]
  [InlineData(5, 320)]
  [InlineData(7, 1280)]
  public void Delay_WithMiddleSample_ShouldDoubleOnEachAttempt(int attempts, int expectedSeconds)
  {
    JobRetryPolicy.Delay(attempts, 0.5).ShouldBe(TimeSpan.FromSeconds(expectedSeconds));
  }

  [Theory]
  [InlineData(8)]
  [InlineData(20)]
  [InlineData(int.MaxValue)]
  public void Delay_AfterManyAttempts_ShouldBeCappedAtThirtyMinutes(int attempts)
  {
    JobRetryPolicy.Delay(attempts, 0.5).ShouldBe(TimeSpan.FromMinutes(30));
  }

  [Fact]
  public void Delay_ShouldBeSpreadByTwentyPercent()
  {
    JobRetryPolicy.Delay(1, 0).ShouldBe(TimeSpan.FromSeconds(16));
    JobRetryPolicy.Delay(1, 0.999999).ShouldBeInRange(TimeSpan.FromSeconds(23.99), TimeSpan.FromSeconds(24));
    JobRetryPolicy.Delay(10, 0).ShouldBe(TimeSpan.FromMinutes(24));
  }

  [Fact]
  public void NextDelay_ShouldStayWithinTheSpread()
  {
    for (var i = 0; i < 100; i++)
    {
      JobRetryPolicy.NextDelay(3).ShouldBeInRange(TimeSpan.FromSeconds(64), TimeSpan.FromSeconds(96));
    }
  }

  [Fact]
  public void Delay_WithInvalidArguments_ShouldThrowArgumentOutOfRangeException()
  {
    Should.Throw<ArgumentOutOfRangeException>(() => JobRetryPolicy.Delay(-1, 0.5));
    Should.Throw<ArgumentOutOfRangeException>(() => JobRetryPolicy.Delay(1, -0.1));
    Should.Throw<ArgumentOutOfRangeException>(() => JobRetryPolicy.Delay(1, 1));
  }

  #endregion
}
