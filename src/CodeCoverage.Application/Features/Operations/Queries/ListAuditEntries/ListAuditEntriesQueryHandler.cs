// ------------------------------------------------------------------------------------------------
// <copyright file="ListAuditEntriesQueryHandler.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using CodeCoverage.Domain.Audit;
using CodeCoverage.Domain.Ports;

namespace CodeCoverage.Application.Features.Operations.Queries.ListAuditEntries;

/// <summary>Handler for the <see cref="ListAuditEntriesQuery" /> query.</summary>
/// <param name="reader">The audit log of the instance.</param>
internal sealed class ListAuditEntriesQueryHandler(IAuditReader reader) : IQueryHandler<ListAuditEntriesQuery, AuditEntryPage>
{
  #region Implementation of IQueryHandler<ListAuditEntriesQuery,AuditEntryPage>

  /// <inheritdoc />
  public async ValueTask<AuditEntryPage> Handle(ListAuditEntriesQuery query, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(query);

    return await reader.ListInstanceAsync(query.Category, query.Search, query.Page, query.PageSize, cancellationToken).ConfigureAwait(false);
  }

  #endregion
}
