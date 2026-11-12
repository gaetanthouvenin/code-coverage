// ------------------------------------------------------------------------------------------------
// <copyright file="RetryJobCommandHandlerTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Application.Exceptions;
using CodeCoverage.Application.Features.Operations;
using CodeCoverage.Application.Features.Operations.Commands.RetryJob;
using CodeCoverage.Domain.Operations;
using CodeCoverage.Domain.Ports;

namespace CodeCoverage.Application.Tests.Features.Operations.Commands.RetryJob;

public sealed class RetryJobCommandHandlerTests
{
  #region Fields

  private readonly IJobQueue _jobQueue = A.Fake<IJobQueue>();

  private readonly IAuditLog _auditLog = A.Fake<IAuditLog>();

  private readonly RetryJobCommandHandler _handler;

  #endregion

  #region Ctors

  public RetryJobCommandHandlerTests()
  {
    _handler = new RetryJobCommandHandler(_jobQueue, _auditLog);
  }

  #endregion

  #region Properties

  private static CancellationToken Ct => TestContext.Current.CancellationToken;

  #endregion

  #region Methods

  [Fact]
  public async Task Handle_RunningJob_ShouldThrowConflict()
  {
    A.CallTo(() => _jobQueue.RetryAsync(7, A<CancellationToken>._)).Returns(JobRetryResult.NotRetryable);

    var exception = await Should.ThrowAsync<ConflictException>(async () => await _handler.Handle(new RetryJobCommand(7), Ct));

    exception.ErrorCode.ShouldBe(OperationsErrors.JobNotRetryable);
    A.CallTo(_auditLog).MustNotHaveHappened();
  }

  [Fact]
  public async Task Handle_UnknownJob_ShouldThrowNotFound()
  {
    A.CallTo(() => _jobQueue.RetryAsync(7, A<CancellationToken>._)).Returns(JobRetryResult.NotFound);

    var exception = await Should.ThrowAsync<NotFoundException>(async () => await _handler.Handle(new RetryJobCommand(7), Ct));

    exception.ErrorCode.ShouldBe(OperationsErrors.JobNotFound);
  }

  [Fact]
  public async Task Handle_DeadJob_ShouldRetryAndRecordIt()
  {
    A.CallTo(() => _jobQueue.RetryAsync(7, A<CancellationToken>._)).Returns(JobRetryResult.Retried);

    await _handler.Handle(new RetryJobCommand(7), Ct);

    A.CallTo(() => _auditLog.RecordAsync(RetryJobCommandHandler.AuditAction, "7", null, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
  }

  #endregion
}
