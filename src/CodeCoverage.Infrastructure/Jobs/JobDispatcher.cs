// ------------------------------------------------------------------------------------------------
// <copyright file="JobDispatcher.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using CodeCoverage.Application.Features.Diagnostics.Commands.RunDiagnosticJob;
using CodeCoverage.Domain.Contracts.Jobs;

namespace CodeCoverage.Infrastructure.Jobs;

/// <summary>
///   <see cref="IJobDispatcher" /> that maps each type of <see cref="JobTypes" /> to the command of the
///   Application it runs, built from the JSON payload of the job.
/// </summary>
/// <param name="sender">The mediator of the scope of the job.</param>
/// <remarks>
///   Each feature that runs work in the background adds its case here, next to its constant in
///   <see cref="JobTypes" /> (job pattern, PLAN §1.4). The commands go through the whole pipeline of the
///   mediator (logging, authorization, validation): the worker sends them as the system principal, which
///   the <c>ISystemOnly</c> commands require.
/// </remarks>
internal sealed class JobDispatcher(ISender sender) : IJobDispatcher
{
  #region Implementation of IJobDispatcher

  /// <inheritdoc />
  public async Task DispatchAsync(JobRecord job, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(job);

    switch (job.Type)
    {
      case JobTypes.Ping:
        await sender.Send(new RunDiagnosticJobCommand(job.Id), cancellationToken).ConfigureAwait(false);
        break;

      default:
        throw new InvalidJobException($"The job type '{job.Type}' is unknown: no command runs it.");
    }
  }

  #endregion
}
