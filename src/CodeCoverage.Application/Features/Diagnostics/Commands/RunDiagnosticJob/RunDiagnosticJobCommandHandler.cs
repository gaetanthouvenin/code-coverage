// ------------------------------------------------------------------------------------------------
// <copyright file="RunDiagnosticJobCommandHandler.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using Microsoft.Extensions.Logging;

namespace CodeCoverage.Application.Features.Diagnostics.Commands.RunDiagnosticJob;

/// <summary>Handler for the <see cref="RunDiagnosticJobCommand" /> command.</summary>
/// <param name="logger">The logger.</param>
internal sealed partial class RunDiagnosticJobCommandHandler(ILogger<RunDiagnosticJobCommandHandler> logger)
  : ICommandHandler<RunDiagnosticJobCommand>
{
  #region Methods

  [LoggerMessage(EventId = 2000, Level = LogLevel.Information, Message = "Diagnostic job {JobId} ran")]
  private static partial void LogRan(ILogger logger, long jobId);

  #endregion

  #region Implementation of ICommandHandler<RunDiagnosticJobCommand,Unit>

  /// <inheritdoc />
  public ValueTask<Unit> Handle(RunDiagnosticJobCommand command, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(command);

    LogRan(logger, command.JobId);

    return ValueTask.FromResult(Unit.Value);
  }

  #endregion
}
