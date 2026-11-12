// ------------------------------------------------------------------------------------------------
// <copyright file="AuditPageViewModel.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Web.ViewModels.Operations;

/// <summary>One page of the audit log of the instance.</summary>
/// <param name="Entries">The entries of the page, the most recent first.</param>
/// <param name="Page">The number of the page, from 1.</param>
/// <param name="PageSize">The number of entries a page holds at most.</param>
/// <param name="TotalCount">The number of entries matching the filter.</param>
public sealed record AuditPageViewModel(IReadOnlyList<AuditEntryViewModel> Entries, int Page, int PageSize, int TotalCount);
