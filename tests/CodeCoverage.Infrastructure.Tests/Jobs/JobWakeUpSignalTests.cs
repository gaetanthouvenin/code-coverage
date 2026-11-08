// ------------------------------------------------------------------------------------------------
// <copyright file="JobWakeUpSignalTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Infrastructure.Jobs;

namespace CodeCoverage.Infrastructure.Tests.Jobs;

public sealed class JobWakeUpSignalTests
{
  #region Properties

  private static CancellationToken Ct => TestContext.Current.CancellationToken;

  #endregion

  #region Methods

  [Fact]
  public async Task Wait_WithoutNotification_ShouldTimeOut()
  {
    var signal = new JobWakeUpSignal(TimeProvider.System);

    (await signal.WaitAsync(TimeSpan.FromMilliseconds(50), Ct)).ShouldBeFalse();
  }

  [Fact]
  public async Task Wait_AfterNotification_ShouldReturnAtOnce()
  {
    var signal = new JobWakeUpSignal(TimeProvider.System);
    signal.Notify(42);

    (await signal.WaitAsync(TimeSpan.Zero, Ct)).ShouldBeTrue();
    (await signal.WaitAsync(TimeSpan.Zero, Ct)).ShouldBeFalse("each notification wakes one loop");
  }

  [Fact]
  public async Task Wait_WhenNotifiedWhileWaiting_ShouldWakeUp()
  {
    var signal = new JobWakeUpSignal(TimeProvider.System);
    var waiting = signal.WaitAsync(TimeSpan.FromMinutes(1), Ct);

    signal.Notify(42);

    (await waiting.WaitAsync(TimeSpan.FromSeconds(10), Ct)).ShouldBeTrue();
  }

  [Fact]
  public async Task Notify_BeyondCapacity_ShouldKeepTheLatestNotifications()
  {
    var signal = new JobWakeUpSignal(TimeProvider.System);

    for (var id = 0; id < JobWakeUpSignal.Capacity * 3; id++)
    {
      signal.Notify(id);
    }

    for (var i = 0; i < JobWakeUpSignal.Capacity; i++)
    {
      (await signal.WaitAsync(TimeSpan.Zero, Ct)).ShouldBeTrue();
    }

    (await signal.WaitAsync(TimeSpan.Zero, Ct)).ShouldBeFalse();
  }

  [Fact]
  public async Task Wait_WhenCanceled_ShouldThrowOperationCanceledException()
  {
    var signal = new JobWakeUpSignal(TimeProvider.System);
    using var cancellation = new CancellationTokenSource();
    var waiting = signal.WaitAsync(TimeSpan.FromMinutes(1), cancellation.Token);

    await cancellation.CancelAsync();

    await Should.ThrowAsync<OperationCanceledException>(() => waiting);
  }

  #endregion
}
