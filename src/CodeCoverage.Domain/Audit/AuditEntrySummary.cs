// ------------------------------------------------------------------------------------------------
// <copyright file="AuditEntrySummary.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Domain.Audit;

/// <summary>An entry of the audit log of the instance, as its page lists it: metadata only, never the details.</summary>
/// <param name="Id">Its identifier.</param>
/// <param name="At">When it happened.</param>
/// <param name="Actor">Who did it.</param>
/// <param name="Action">What was done, e.g. <c>Settings.Update</c>.</param>
/// <param name="Target">What it was done to: a key, a login, an identifier; never a value.</param>
/// <param name="Category">Its family, or <see langword="null" /> when no family holds its action.</param>
/// <remarks>A read model: no secret value, no detail.</remarks>
public sealed record AuditEntrySummary(long Id, DateTimeOffset At, string Actor, string Action, string Target, AuditCategory? Category);
