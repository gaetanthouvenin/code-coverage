// ------------------------------------------------------------------------------------------------
// <copyright file="JobDispatcherTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using CodeCoverage.Application.Features.Diagnostics.Commands.RunDiagnosticJob;
using CodeCoverage.Domain.Contracts.Jobs;
using CodeCoverage.Domain.Operations;
using CodeCoverage.Infrastructure.Jobs;

namespace CodeCoverage.Infrastructure.Tests.Jobs;

public sealed class JobDispatcherTests
{
  #region Fields

  private readonly ISender _sender = A.Fake<ISender>();

  #endregion

  #region Properties

  private static CancellationToken Ct => TestContext.Current.CancellationToken;

  #endregion

  #region Methods

  [Fact]
  public async Task Dispatch_Ping_ShouldSendTheDiagnosticCommand()
  {
    var dispatcher = new JobDispatcher(_sender);

    await dispatcher.DispatchAsync(Job(JobTypes.Ping, 42), Ct);

    A.CallTo(() => _sender.Send(new RunDiagnosticJobCommand(42), Ct)).MustHaveHappenedOnceExactly();
  }

  [Fact]
  public async Task Dispatch_UnknownType_ShouldThrowInvalidJobException()
  {
    var dispatcher = new JobDispatcher(_sender);

    var exception = await Should.ThrowAsync<InvalidJobException>(() => dispatcher.DispatchAsync(Job("Nope", 7), Ct));

    exception.Message.ShouldContain("Nope");
    A.CallTo(_sender).MustNotHaveHappened();
  }

  [Fact]
  public async Task Dispatch_WithNullJob_ShouldThrowArgumentNullException()
  {
    await Should.ThrowAsync<ArgumentNullException>(() => new JobDispatcher(_sender).DispatchAsync(null!, Ct));
  }

  private static JobRecord Job(string type, long id) =>
    new()
    {
      Id = id,
      Type = type,
      PayloadJson = "{}",
      State = JobState.Running,
      CorrelationId = "test",
    };

  #endregion
}
