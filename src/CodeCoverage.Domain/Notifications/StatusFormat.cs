// ------------------------------------------------------------------------------------------------
// <copyright file="StatusFormat.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Config;

namespace CodeCoverage.Domain.Notifications;

/// <summary>How the statuses of a commit are named and how their numbers are written.</summary>
/// <param name="Prefix">The prefix of the contexts.</param>
/// <param name="Precision">The number of decimals (<c>coverage.precision</c>).</param>
/// <param name="Rounding">The rounding (<c>coverage.round</c>).</param>
public sealed record StatusFormat(string Prefix, int Precision, RoundingMode Rounding);