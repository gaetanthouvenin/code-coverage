// ------------------------------------------------------------------------------------------------
// <copyright file="AuditEntryPage.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Domain.Audit;

/// <summary>One page of the audit log of the instance, the most recent first.</summary>
/// <param name="Items">The entries of the page.</param>
/// <param name="Page">The number of the page, from 1.</param>
/// <param name="PageSize">The number of entries a page holds at most.</param>
/// <param name="TotalCount">The number of entries matching the filter.</param>
public sealed record AuditEntryPage(IReadOnlyList<AuditEntrySummary> Items, int Page, int PageSize, int TotalCount);
