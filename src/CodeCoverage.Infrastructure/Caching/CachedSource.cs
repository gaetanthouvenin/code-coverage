// ------------------------------------------------------------------------------------------------
// <copyright file="CachedSource.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Infrastructure.Caching;

/// <summary>A source kept by <see cref="BoundedSourceContentCache" />: its text, or the verdict that it cannot be shown.</summary>
/// <param name="Text">The text, or <see langword="null" /> when the source is unavailable.</param>
internal sealed record CachedSource(string? Text);
