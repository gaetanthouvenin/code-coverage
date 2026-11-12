// ------------------------------------------------------------------------------------------------
// <copyright file="ListOrganizationsQueryHandler.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Ports;

namespace CodeCoverage.Application.Features.Organizations.Queries.ListOrganizations;

/// <summary>Handler for the <see cref="ListOrganizationsQuery" /> query.</summary>
/// <param name="directory">The organizations of the instance.</param>
/// <param name="jobs">The metadata of the queue.</param>
internal sealed class ListOrganizationsQueryHandler(IOrganizationDirectory directory, IJobMonitor jobs)
  : IQueryHandler<ListOrganizationsQuery, IReadOnlyList<OrganizationOverview>>
{
  #region Implementation of IQueryHandler<ListOrganizationsQuery,IReadOnlyList<OrganizationOverview>>

  /// <inheritdoc />
  public async ValueTask<IReadOnlyList<OrganizationOverview>> Handle(ListOrganizationsQuery query, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(query);

    var organizations = await directory.ListAllAsync(cancellationToken).ConfigureAwait(false);
    var depths = await jobs.CountQueuedByOrganizationAsync(cancellationToken).ConfigureAwait(false);

    return
    [
      .. organizations.Where(static o => !o.Login.StartsWith(Organization.TombstonePrefix, StringComparison.Ordinal))
                      .OrderBy(static o => o.NormalizedLogin, StringComparer.Ordinal)
                      .ThenBy(static o => o.Forge)
                      .Select(o => new OrganizationOverview(o, depths.GetValueOrDefault(o.Id))),
    ];
  }

  #endregion
}
