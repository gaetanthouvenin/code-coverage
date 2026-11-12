// ------------------------------------------------------------------------------------------------
// <copyright file="GetSystemStatusQueryHandler.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using CodeCoverage.Domain.Operations;
using CodeCoverage.Domain.Ports;

namespace CodeCoverage.Application.Features.Operations.Queries.GetSystemStatus;

/// <summary>Handler for the <see cref="GetSystemStatusQuery" /> query.</summary>
/// <param name="monitor">Reads the job queue.</param>
internal sealed class GetSystemStatusQueryHandler(IJobMonitor monitor) : IQueryHandler<GetSystemStatusQuery, SystemStatus>
{
  #region Implementation of IQueryHandler<GetSystemStatusQuery,SystemStatus>

  /// <inheritdoc />
  public async ValueTask<SystemStatus> Handle(GetSystemStatusQuery query, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(query);

    return await monitor.GetStatusAsync(cancellationToken).ConfigureAwait(false);
  }

  #endregion
}
