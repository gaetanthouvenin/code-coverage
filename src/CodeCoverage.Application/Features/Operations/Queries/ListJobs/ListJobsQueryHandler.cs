// ------------------------------------------------------------------------------------------------
// <copyright file="ListJobsQueryHandler.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using CodeCoverage.Domain.Operations;
using CodeCoverage.Domain.Ports;

namespace CodeCoverage.Application.Features.Operations.Queries.ListJobs;

/// <summary>Handler for the <see cref="ListJobsQuery" /> query.</summary>
/// <param name="monitor">Reads the job queue.</param>
internal sealed class ListJobsQueryHandler(IJobMonitor monitor) : IQueryHandler<ListJobsQuery, JobPage>
{
  #region Implementation of IQueryHandler<ListJobsQuery,JobPage>

  /// <inheritdoc />
  public async ValueTask<JobPage> Handle(ListJobsQuery query, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(query);

    return await monitor.ListAsync(query.State, query.Page, query.PageSize, cancellationToken).ConfigureAwait(false);
  }

  #endregion
}
