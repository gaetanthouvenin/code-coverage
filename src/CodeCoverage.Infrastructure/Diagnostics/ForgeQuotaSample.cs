// ------------------------------------------------------------------------------------------------
// <copyright file="ForgeQuotaSample.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Organizations;

namespace CodeCoverage.Infrastructure.Diagnostics;

/// <summary>
///   The quota of a connection of a forge whose size is known, as a row of <c>ForgeQuotas</c> last recorded it, for the
///   gauges <see cref="CodeCoverageMeter.ForgeRateLimitLimit" /> and
///   <see cref="CodeCoverageMeter.ForgeRateLimitRecordedRemaining" /> (M7.29).
/// </summary>
/// <param name="Forge">The forge of the quota.</param>
/// <param name="Connection">The connection: a GitHub installation, a forge connection or the application itself.</param>
/// <param name="Limit">The requests of the whole window.</param>
/// <param name="Remaining">The requests left; the whole <paramref name="Limit" /> once the reset time has passed.</param>
internal sealed record ForgeQuotaSample(Forge Forge, string Connection, long Limit, long Remaining);
