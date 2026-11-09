// ------------------------------------------------------------------------------------------------
// <copyright file="ParsedYaml.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Config;

namespace CodeCoverage.Application.Services;

/// <summary>What a YAML layer holds.</summary>
/// <param name="Root">Its content, or <see langword="null" /> when there is none or it cannot be read.</param>
/// <param name="Error">Why it cannot be read, if so.</param>
/// <param name="Warnings">What is not applied as written.</param>
internal sealed record ParsedYaml(ConfigNode? Root, string? Error, IReadOnlyList<ConfigWarning> Warnings);
