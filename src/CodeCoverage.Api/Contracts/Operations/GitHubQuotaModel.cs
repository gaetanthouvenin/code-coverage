// ------------------------------------------------------------------------------------------------
// <copyright file="GitHubQuotaModel.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Api.Contracts.Operations;

/// <summary>The last GitHub quota of an installation, or of the App (<c>app</c>).</summary>
/// <param name="Installation">The installation, or <c>app</c>.</param>
/// <param name="Remaining">The requests left.</param>
/// <param name="Limit">The requests of the whole window, or <see langword="null" /> when GitHub did not say.</param>
/// <param name="ResetAt">When the window starts again, or <see langword="null" />.</param>
/// <param name="ObservedAt">When GitHub reported it.</param>
public sealed record GitHubQuotaModel(string Installation, long Remaining, long? Limit, DateTimeOffset? ResetAt, DateTimeOffset ObservedAt);
