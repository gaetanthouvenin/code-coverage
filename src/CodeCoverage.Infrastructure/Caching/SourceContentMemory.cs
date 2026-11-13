// ------------------------------------------------------------------------------------------------
// <copyright file="SourceContentMemory.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.Extensions.Caching.Memory;

namespace CodeCoverage.Infrastructure.Caching;

/// <summary>
///   The memory of the sources read from the forges, bounded in size: a singleton of its own, so the bound never applies
///   to the shared <see cref="IMemoryCache" />, whose users set no entry size.
/// </summary>
/// <remarks>
///   Shared by every organization of the process: <see cref="BoundedSourceContentCache" />, its only user, starts each
///   key with the organization of the scope (ADR-013).
/// </remarks>
internal sealed class SourceContentMemory : IDisposable
{
  #region Fields

  /// <summary>The largest size of the memory, in characters: about 100 MB of text.</summary>
  public const long SizeLimit = 50L * 1024 * 1024;

  #endregion

  #region Properties

  /// <summary>Gets the cache.</summary>
  public MemoryCache Cache { get; } = new(new MemoryCacheOptions { SizeLimit = SizeLimit, TrackStatistics = true });

  #endregion

  #region Implementation of IDisposable

  /// <inheritdoc />
  public void Dispose() => Cache.Dispose();

  #endregion
}
