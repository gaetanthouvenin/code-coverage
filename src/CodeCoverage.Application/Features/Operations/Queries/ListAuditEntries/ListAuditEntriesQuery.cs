// ------------------------------------------------------------------------------------------------
// <copyright file="ListAuditEntriesQuery.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using CodeCoverage.Application.Security;
using CodeCoverage.Domain.Audit;

namespace CodeCoverage.Application.Features.Operations.Queries.ListAuditEntries;

/// <summary>Lists one page of the audit log of the instance: metadata only, never an entry of an organization.</summary>
/// <param name="Category">The family kept, or <see langword="null" /> for every entry.</param>
/// <param name="Search">A part of the actor, the action or the target to look for, or <see langword="null" />.</param>
/// <param name="Page">The number of the page, from 1.</param>
/// <param name="PageSize">The number of entries of a page.</param>
public sealed record ListAuditEntriesQuery(AuditCategory? Category, string? Search, int Page = 1, int PageSize = ListAuditEntriesQuery.DefaultPageSize)
  : IQuery<AuditEntryPage>, IAdminOnly
{
  #region Fields

  /// <summary>The number of entries of a page when none is asked.</summary>
  public const int DefaultPageSize = 25;

  /// <summary>The largest number of entries of a page.</summary>
  public const int MaxPageSize = 100;

  /// <summary>The last page that can be asked.</summary>
  public const int MaxPage = int.MaxValue / MaxPageSize;

  /// <summary>The longest search.</summary>
  public const int SearchMaxLength = 200;

  #endregion
}
