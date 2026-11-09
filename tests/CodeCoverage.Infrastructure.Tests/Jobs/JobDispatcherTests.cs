// ------------------------------------------------------------------------------------------------
// <copyright file="JobDispatcherTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using CodeCoverage.Application.Features.Diagnostics.Commands.RunDiagnosticJob;
using CodeCoverage.Application.Features.Uploads.Commands.ProcessUpload;
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
  public async Task Dispatch_ProcessUpload_ShouldSendTheCommandOfTheStoredUpload()
  {
    var uploadId = Guid.NewGuid();
    var job = Job(JobTypes.ProcessUpload, 42);
    job.PayloadJson = $$"""{"UploadId":"{{uploadId}}"}""";
    job.LockedBy = "worker-1";

    await new JobDispatcher(_sender).DispatchAsync(job, Ct);

    A.CallTo(() => _sender.Send(new ProcessUploadCommand(uploadId, "worker-1"), Ct)).MustHaveHappenedOnceExactly();
  }

  [Theory]
  [InlineData("{}")]
  [InlineData("not json")]
  [InlineData("""{"UploadId":"not-a-guid"}""")]
  public async Task Dispatch_ProcessUploadWithInvalidPayload_ShouldThrowInvalidJobException(string payload)
  {
    var job = Job(JobTypes.ProcessUpload, 42);
    job.PayloadJson = payload;

    await Should.ThrowAsync<InvalidJobException>(() => new JobDispatcher(_sender).DispatchAsync(job, Ct));
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
