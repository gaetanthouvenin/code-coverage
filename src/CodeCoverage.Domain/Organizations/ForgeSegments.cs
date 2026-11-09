// ------------------------------------------------------------------------------------------------
// <copyright file="ForgeSegments.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Diagnostics.CodeAnalysis;

namespace CodeCoverage.Domain.Organizations;

/// <summary>How a forge is written in the addresses of the product and in the fields of an upload (ADR-014).</summary>
public static class ForgeSegments
{
  #region Methods

  /// <summary>Reads a forge.</summary>
  /// <param name="segment">The text, whatever its case: a name or its alias.</param>
  /// <param name="forge">The forge, when the text names one.</param>
  /// <returns><see langword="true" /> when the text names a forge.</returns>
  public static bool TryParse([NotNullWhen(true)] string? segment, out Forge forge)
  {
    switch (segment?.ToLowerInvariant())
    {
      case "github" or "gh":
        forge = Forge.GitHub;
        return true;
      case "bitbucket" or "bb":
        forge = Forge.Bitbucket;
        return true;
      case "azure-devops":
        forge = Forge.AzureDevOps;
        return true;
      case "gitlab" or "gl":
        forge = Forge.GitLab;
        return true;
      default:
        forge = default;
        return false;
    }
  }

  /// <summary>Writes a forge.</summary>
  /// <param name="forge">The forge.</param>
  /// <returns>The segment of its addresses.</returns>
  /// <exception cref="ArgumentOutOfRangeException"><paramref name="forge" /> is not a known forge.</exception>
  public static string ToSegment(Forge forge)
  {
    return forge switch
    {
      Forge.GitHub => "github",
      Forge.Bitbucket => "bitbucket",
      Forge.AzureDevOps => "azure-devops",
      Forge.GitLab => "gitlab",
      var _ => throw new ArgumentOutOfRangeException(nameof(forge), forge, "The forge is not known."),
    };
  }

  #endregion
}
