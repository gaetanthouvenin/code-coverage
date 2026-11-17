// ------------------------------------------------------------------------------------------------
// <copyright file="ForgeLinkResolver.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Ports;

namespace CodeCoverage.Web.Mapping;

/// <summary>Hands the mappers the links of a forge, so a page passes it on without naming a Domain type (rule 5).</summary>
/// <param name="forges">Resolves the links of each forge.</param>
/// <remarks>
///   A page injects it and gives it to the mapper extension it sends through; the mapper alone asks it for the
///   <see cref="IForgeLinks" /> of the forge of the address.
/// </remarks>
public sealed class ForgeLinkResolver(IForgeGatewayProvider forges)
{
  #region Methods

  /// <summary>Finds the links of a forge.</summary>
  /// <param name="forge">The forge.</param>
  /// <returns>The links of the forge, or <see langword="null" /> for a forge prepared by ADR-014 with no adapter yet.</returns>
  internal IForgeLinks? Find(Forge forge)
  {
    try
    {
      return forges.GetLinks(forge);
    }
    catch (NotSupportedException)
    {
      // A forge with no adapter yet builds no link: the page shows none rather than a wrong one.
      return null;
    }
  }

  #endregion
}
