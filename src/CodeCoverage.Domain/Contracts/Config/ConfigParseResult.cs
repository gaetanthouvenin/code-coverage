// ------------------------------------------------------------------------------------------------
// <copyright file="ConfigParseResult.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Config;

namespace CodeCoverage.Domain.Contracts.Config;

/// <summary>What reading a configuration file gives.</summary>
/// <param name="Root">The content of the file, or <see langword="null" /> if it cannot be read.</param>
/// <param name="Error">Why the file cannot be read, if so.</param>
/// <param name="Warnings">The keys that are unknown, without effect or ignored.</param>
public sealed record ConfigParseResult(ConfigNode? Root, string? Error, IReadOnlyList<ConfigWarning> Warnings);
