// ------------------------------------------------------------------------------------------------
// <copyright file="AdministratorList.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Globalization;

using CodeCoverage.Domain.Organizations;

namespace CodeCoverage.Domain.Users;

/// <summary>
///   Reads the lists of the administrators of the instance: numeric GitHub identifiers separated by commas, as
///   <c>Access:AdminUserIds</c> and <c>CodeCoverage__Bootstrap__AdminUserIds</c> hold them.
/// </summary>
/// <remarks>
///   The one parser of both lists, so that the sign-in and the principal of every request agree. An entry that is not a
///   number is ignored, never an error: a typing mistake in a list grants nothing to anyone.
/// </remarks>
public static class AdministratorList
{
  #region Methods

  /// <summary>Tells whether an account is named by one of the lists.</summary>
  /// <param name="forge">The forge of the account.</param>
  /// <param name="externalId">The identifier the forge gives the account.</param>
  /// <param name="lists">The lists; a <see langword="null" /> or empty one names nobody.</param>
  /// <returns>
  ///   <see langword="true" /> when the account is a GitHub account whose identifier one of the lists holds; the lists
  ///   name GitHub accounts only.
  /// </returns>
  /// <exception cref="ArgumentNullException"><paramref name="externalId" /> is <see langword="null" />.</exception>
  public static bool Contains(Forge forge, string externalId, params ReadOnlySpan<string?> lists)
  {
    ArgumentNullException.ThrowIfNull(externalId);

    if (forge is not Forge.GitHub || !TryParse(externalId, out var id))
    {
      return false;
    }

    foreach (var list in lists)
    {
      if (list is null)
      {
        continue;
      }

      foreach (var entry in list.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
      {
        if (TryParse(entry, out var listed) && listed == id)
        {
          return true;
        }
      }
    }

    return false;
  }

  /// <summary>Reads one numeric identifier.</summary>
  /// <param name="value">The text.</param>
  /// <param name="id">The identifier read.</param>
  /// <returns><see langword="true" /> when the text is a positive number made of ASCII digits only.</returns>
  private static bool TryParse(string value, out long id) =>
    long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out id) && id > 0;

  #endregion
}
