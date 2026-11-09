// ------------------------------------------------------------------------------------------------
// <copyright file="OracleLine.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Coverage;

namespace CodeCoverage.Infrastructure.Tests.Reports.Oracle;

internal readonly record struct OracleLine(LineState State, int CoveredBranches, int TotalBranches);
