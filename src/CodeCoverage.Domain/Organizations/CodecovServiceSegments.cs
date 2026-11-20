// ------------------------------------------------------------------------------------------------
// <copyright file="CodecovServiceSegments.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;

namespace CodeCoverage.Domain.Organizations;

/// <summary>How Codecov writes a forge in the addresses of its badges: one row per forge it serves (ADR-014).</summary>
/// <remarks>
///   The routes compatible with Codecov and the badge snippets are drawn from this one table. A forge without a row
///   (Azure DevOps, which Codecov does not serve) only has the neutral route of the API.
/// </remarks>
public static class CodecovServiceSegments
{
  #region Fields

  /// <summary>The segment the badge snippets write, by forge.</summary>
  private static readonly FrozenDictionary<Forge, string> Segments = new Dictionary<Forge, string>
  {
    [Forge.GitHub] = "github",
  }.ToFrozenDictionary();

  /// <summary>Every segment Codecov accepts, whatever its case, with the forge it names.</summary>
  private static readonly FrozenDictionary<string, Forge> Services = new Dictionary<string, Forge>(StringComparer.OrdinalIgnoreCase)
  {
    ["gh"] = Forge.GitHub,
    ["github"] = Forge.GitHub,
  }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

  #endregion

  #region Methods

  /// <summary>Reads the service segment of an address of Codecov.</summary>
  /// <param name="segment">The text, whatever its case: the name of the forge or its alias.</param>
  /// <param name="forge">The forge, when the text is a segment of Codecov.</param>
  /// <returns><see langword="true" /> when the text is a segment of Codecov.</returns>
  public static bool TryParse([NotNullWhen(true)] string? segment, out Forge forge)
  {
    if (segment is not null && Services.TryGetValue(segment, out forge))
    {
      return true;
    }

    forge = default;

    return false;
  }

  /// <summary>Gives the segment of Codecov a badge snippet writes for a forge.</summary>
  /// <param name="forge">The forge.</param>
  /// <param name="segment">The segment, when Codecov serves the forge.</param>
  /// <returns><see langword="true" /> when the forge has a row in the table.</returns>
  public static bool TryGetSegment(Forge forge, [NotNullWhen(true)] out string? segment) => Segments.TryGetValue(forge, out segment);

  #endregion
}
