// ------------------------------------------------------------------------------------------------
// <copyright file="FileCoverageView.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Domain.Coverage;

/// <summary>The coverage of one file of a commit, line by line, for all its sessions or those of one flag.</summary>
/// <param name="File">The file, its lines and its totals.</param>
/// <param name="Flag">The flag the lines are read for, or <see langword="null" /> for the merged report.</param>
/// <remarks>A read model.</remarks>
public sealed record FileCoverageView(FileCoverage File, string? Flag);
