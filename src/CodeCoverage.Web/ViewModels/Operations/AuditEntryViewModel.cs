// ------------------------------------------------------------------------------------------------
// <copyright file="AuditEntryViewModel.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Web.ViewModels.Operations;

/// <summary>An entry of the audit log of the instance: metadata only.</summary>
/// <param name="Id">Its identifier.</param>
/// <param name="At">When it happened.</param>
/// <param name="Actor">Who did it.</param>
/// <param name="Action">The raw code of the action, shown when no label names it.</param>
/// <param name="Label">The key of the label of the action in the resources, or <see langword="null" /> for an action no family holds.</param>
/// <param name="Family">The name of its family (<c>Secrets</c>, <c>Settings</c>…), or <see langword="null" />.</param>
/// <param name="Target">What it was done to: a key, a login, an identifier.</param>
public sealed record AuditEntryViewModel(long Id, DateTimeOffset At, string Actor, string Action, string? Label, string? Family, string Target);
