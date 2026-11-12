// ------------------------------------------------------------------------------------------------
// <copyright file="RetryJobCommandHandler.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Globalization;

using Mediator;

using CodeCoverage.Application.Exceptions;
using CodeCoverage.Domain.Ports;

namespace CodeCoverage.Application.Features.Operations.Commands.RetryJob;

/// <summary>Handler for the <see cref="RetryJobCommand" /> command.</summary>
/// <param name="jobQueue">The job queue, which resets the row.</param>
/// <param name="auditLog">The audit trail.</param>
internal sealed class RetryJobCommandHandler(IJobQueue jobQueue, IAuditLog auditLog) : ICommandHandler<RetryJobCommand>
{
  #region Fields

  /// <summary>The audit action of a retried job.</summary>
  internal const string AuditAction = "Jobs.Retry";

  #endregion

  #region Implementation of ICommandHandler<RetryJobCommand>

  /// <inheritdoc />
  /// <exception cref="NotFoundException">No job has this identifier (<see cref="OperationsErrors.JobNotFound" />).</exception>
  /// <exception cref="ConflictException">The job is not failed or dead, or its twin already waits (<see cref="OperationsErrors.JobNotRetryable" />).</exception>
  public async ValueTask<Unit> Handle(RetryJobCommand command, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(command);

    var result = await jobQueue.RetryAsync(command.Id, cancellationToken).ConfigureAwait(false);

    NotFoundException.ThrowIfJobNotFound(result);
    ConflictException.ThrowIfJobNotRetryable(result);

    await auditLog.RecordAsync(AuditAction, command.Id.ToString(CultureInfo.InvariantCulture), null, cancellationToken).ConfigureAwait(false);

    return Unit.Value;
  }

  #endregion
}
