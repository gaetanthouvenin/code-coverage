// ------------------------------------------------------------------------------------------------
// <copyright file="ParseGateTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.Extensions.Options;

using CodeCoverage.Application.Configuration;
using CodeCoverage.Application.Services;

namespace CodeCoverage.Application.Tests.Services;

public sealed class ParseGateTests
{
  #region Methods

  [Fact]
  public async Task Enter_OverTheLimit_ShouldWaitForAPlace()
  {
    var gate = Gate(1);
    var first = await gate.EnterAsync(TestContext.Current.CancellationToken);

    var second = gate.EnterAsync(TestContext.Current.CancellationToken);

    second.IsCompleted.ShouldBeFalse();

    first.Dispose();
    (await second.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken)).Dispose();
  }

  [Fact]
  public async Task Dispose_Twice_ShouldGiveThePlaceOnce()
  {
    var gate = Gate(1);
    var first = await gate.EnterAsync(TestContext.Current.CancellationToken);

    first.Dispose();
    first.Dispose();

    var second = await gate.EnterAsync(TestContext.Current.CancellationToken);
    var third = gate.EnterAsync(TestContext.Current.CancellationToken);

    third.IsCompleted.ShouldBeFalse();
    second.Dispose();
    (await third.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken)).Dispose();
  }

  private static ParseGate Gate(int limit)
  {
    var options = A.Fake<IOptionsMonitor<ProcessingOptions>>();
    A.CallTo(() => options.CurrentValue).Returns(new ProcessingOptions { MaxParallelParses = limit });

    return new ParseGate(options);
  }

  #endregion
}
