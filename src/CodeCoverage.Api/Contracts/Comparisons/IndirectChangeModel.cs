// ------------------------------------------------------------------------------------------------
// <copyright file="IndirectChangeModel.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Api.Contracts.Comparisons;

/// <summary>A file whose coverage changed although the change does not touch it.</summary>
/// <param name="Path">The path of the file.</param>
/// <param name="Kind">How it changed.</param>
/// <param name="DeltaHits">The change of its covered lines.</param>
/// <param name="DeltaMisses">The change of its lines that are not covered.</param>
/// <param name="DeltaPartials">The change of its partially covered lines.</param>
/// <param name="DeltaCoverage">The change of its coverage in points, or <see langword="null" />.</param>
public sealed record IndirectChangeModel(string Path, string Kind, int DeltaHits, int DeltaMisses, int DeltaPartials, decimal? DeltaCoverage);
