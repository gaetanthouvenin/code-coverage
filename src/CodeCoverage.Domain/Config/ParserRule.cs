// ------------------------------------------------------------------------------------------------
// <copyright file="ParserRule.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Domain.Config;

/// <summary>The options of one report format (<c>parsers.&lt;format&gt;</c>).</summary>
/// <param name="PartialsAsHits">A value indicating whether a partial line counts as a hit.</param>
/// <param name="HandleMissingConditions">
///   A value indicating whether the conditions missing from a report are deduced, which only applies to
///   Cobertura.
/// </param>
public sealed record ParserRule(bool PartialsAsHits, bool HandleMissingConditions);
