// ------------------------------------------------------------------------------------------------
// <copyright file="LoggingBehaviorTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.Extensions.Logging;

using CodeCoverage.Application.Behaviors;
using CodeCoverage.Application.Exceptions;
using CodeCoverage.Application.Features.Diagnostics.Queries.Ping;
using CodeCoverage.Application.Tests.Behaviors.Support;

namespace CodeCoverage.Application.Tests.Behaviors;

public sealed class LoggingBehaviorTests
{
  #region Fields

  private const string Secret = "cvc_9f8e7d6c5b4a39281706f5e4d3c2b1a0";

  private readonly PipelineTrace _trace = new();

  #endregion

  #region Methods

  [Fact]
  public async Task Handle_WithQuery_ShouldLogAtDebug()
  {
    var behavior = new LoggingBehavior<PingQuery, string>(new RecordingLogger<LoggingBehavior<PingQuery, string>>(_trace));

    var response = await behavior.Handle(
                     new PingQuery(),
                     static (_, _) => ValueTask.FromResult("1.0.0"),
                     TestContext.Current.CancellationToken);

    response.ShouldBe("1.0.0");
    _trace.Logs.Count.ShouldBe(2);
    _trace.Logs.ShouldAllBe(l => l.Level == LogLevel.Debug);
    _trace.Logs[0].Message.ShouldBe("Handling PingQuery");
    _trace.Logs[1].Message.ShouldStartWith("Handled PingQuery in ");
  }

  [Fact]
  public async Task Handle_WithCommand_ShouldLogAtInformation()
  {
    var behavior = CreateCommandBehavior();

    await behavior.Handle(
      new SampleCommand("main", Secret),
      static (_, _) => ValueTask.FromResult("done"),
      TestContext.Current.CancellationToken);

    _trace.Logs.Count.ShouldBe(2);
    _trace.Logs.ShouldAllBe(l => l.Level == LogLevel.Information);
    _trace.Logs[0].Message.ShouldBe("Handling SampleCommand");
  }

  [Fact]
  public async Task Handle_WithRefusal_ShouldLogWarningWithErrorCode()
  {
    var behavior = CreateCommandBehavior();
    var refusal = new ConflictException("Upload.AlreadyProcessed", "The upload is already processed.");

    var thrown = await Should.ThrowAsync<ConflictException>(
                   async () => await behavior.Handle(
                                 new SampleCommand("main", Secret),
                                 (_, _) => throw refusal,
                                 TestContext.Current.CancellationToken));

    thrown.ShouldBeSameAs(refusal);
    var warning = _trace.Logs.Where(l => l.Level == LogLevel.Warning).ShouldHaveSingleItem();
    warning.Message.ShouldStartWith("Refused SampleCommand after ");
    warning.Message.ShouldEndWith(": Upload.AlreadyProcessed");
    warning.Values.ShouldContain(new KeyValuePair<string, object?>("ErrorCode", "Upload.AlreadyProcessed"));
    warning.Exception.ShouldBeNull();
    _trace.Logs.ShouldNotContain(l => l.Level == LogLevel.Error);
  }

  [Fact]
  public async Task Handle_WithUnexpectedException_ShouldLogError()
  {
    var behavior = CreateCommandBehavior();
    var failure = new InvalidOperationException("The database is gone.");

    await Should.ThrowAsync<InvalidOperationException>(
      async () => await behavior.Handle(
                    new SampleCommand("main", Secret),
                    (_, _) => throw failure,
                    TestContext.Current.CancellationToken));

    var error = _trace.Logs.Where(l => l.Level == LogLevel.Error).ShouldHaveSingleItem();
    error.Message.ShouldStartWith("Failed SampleCommand after ");
    error.Exception.ShouldBeSameAs(failure);
    _trace.Logs.ShouldNotContain(l => l.Level == LogLevel.Warning);
  }

  [Fact]
  public async Task Handle_ShouldNeverLogMessageProperties()
  {
    var behavior = CreateCommandBehavior();
    var command = new SampleCommand("main", Secret);

    await behavior.Handle(command, static (_, _) => ValueTask.FromResult("done"), TestContext.Current.CancellationToken);
    await Should.ThrowAsync<ConflictException>(
      async () => await behavior.Handle(
                    command,
                    static (_, _) => throw new ConflictException("Sample.Refused", "Refused."),
                    TestContext.Current.CancellationToken));
    await Should.ThrowAsync<InvalidOperationException>(
      async () => await behavior.Handle(
                    command,
                    static (_, _) => throw new InvalidOperationException("Failed."),
                    TestContext.Current.CancellationToken));

    _trace.Logs.Count.ShouldBe(6);

    foreach (var log in _trace.Logs)
    {
      log.Message.ShouldNotContain(Secret);
      log.Message.ShouldNotContain(command.ToString());

      foreach (var value in log.Values)
      {
        value.Value.ShouldNotBeOfType<SampleCommand>();
        (value.Value?.ToString() ?? string.Empty).ShouldNotContain(Secret);
      }
    }
  }

  [Fact]
  public async Task Handle_WithNullNext_ShouldThrowArgumentNullException()
  {
    var behavior = CreateCommandBehavior();

    await Should.ThrowAsync<ArgumentNullException>(
      async () => await behavior.Handle(new SampleCommand("main", Secret), null!, TestContext.Current.CancellationToken));
  }

  private LoggingBehavior<SampleCommand, string> CreateCommandBehavior() =>
    new(new RecordingLogger<LoggingBehavior<SampleCommand, string>>(_trace));

  #endregion
}
