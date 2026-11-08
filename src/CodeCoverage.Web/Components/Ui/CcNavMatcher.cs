// ------------------------------------------------------------------------------------------------
// <copyright file="CcNavMatcher.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;

using CodeCoverage.Web.Components.Ui.Models;

namespace CodeCoverage.Web.Components.Ui;

/// <summary>Decides which <see cref="CcNavItem" /> the current address activates.</summary>
/// <remarks>
///   The rule is the one of <see cref="NavLink" />, computed here so that the components set both
///   their active class and <c>aria-current="page"</c>.
/// </remarks>
internal static class CcNavMatcher
{
  #region Methods

  /// <summary>Indicates whether the current address activates a link.</summary>
  /// <param name="navigation">The navigation manager that knows the current address.</param>
  /// <param name="item">The link.</param>
  /// <returns>
  ///   <see langword="true" /> when the path equals the address of the link, or, with
  ///   <see cref="NavLinkMatch.Prefix" />, starts with it followed by <c>/</c>.
  /// </returns>
  /// <exception cref="ArgumentNullException">
  ///   <paramref name="navigation" /> or <paramref name="item" /> is <see langword="null" />.
  /// </exception>
  public static bool IsActive(NavigationManager navigation, CcNavItem item)
  {
    ArgumentNullException.ThrowIfNull(navigation);
    ArgumentNullException.ThrowIfNull(item);

    var current = Normalize(navigation.ToBaseRelativePath(navigation.Uri));
    var target = Normalize(item.Href);

    if (string.Equals(current, target, StringComparison.OrdinalIgnoreCase))
    {
      return true;
    }

    return item.Match == NavLinkMatch.Prefix
           && target.Length > 0
           && current.StartsWith(target + "/", StringComparison.OrdinalIgnoreCase);
  }

  private static string Normalize(string path)
  {
    var end = path.IndexOfAny(['?', '#']);
    var withoutQuery = end < 0 ? path : path[..end];

    return withoutQuery.Trim('/');
  }

  #endregion
}
