// ------------------------------------------------------------------------------------------------
// <copyright file="ComparisonGuards.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Diagnostics.CodeAnalysis;

using CodeCoverage.Application.Exceptions;
using CodeCoverage.Domain.Comparisons;

namespace CodeCoverage.Application.Features.Comparisons;

/// <summary>
///   The refusals of the comparisons, in the form of
///   <see cref="ArgumentNullException.ThrowIfNull(object?, string?)" />: one place holds the code and the
///   message of each.
/// </summary>
internal static class ComparisonGuards
{
  #region Methods

  extension(NotFoundException)
  {
    /// <summary>Throws when no comparison is stored for what a read asks.</summary>
    /// <param name="comparison">The comparison that was looked for.</param>
    /// <exception cref="NotFoundException"><paramref name="comparison" /> is <see langword="null" />.</exception>
    public static void ThrowIfComparisonNotComputed([NotNull] CommitComparison? comparison)
    {
      if (comparison is null)
      {
        throw new NotFoundException(ComparisonErrors.NotComputed, "No comparison of these commits is computed yet.");
      }
    }
  }

  #endregion
}
