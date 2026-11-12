// ------------------------------------------------------------------------------------------------
// <copyright file="BadgeEntityTag.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Security.Cryptography;
using System.Text;

using Microsoft.Extensions.Primitives;

using CodeCoverage.Api.Contracts.Badges;

namespace CodeCoverage.Api.Rendering;

/// <summary>The entity tag of a badge: the hash of what it shows and of its format.</summary>
internal static class BadgeEntityTag
{
  #region Methods

  /// <summary>Gives the entity tag of a badge in a format.</summary>
  /// <param name="badge">The badge.</param>
  /// <param name="extension">The format: <c>svg</c> or <c>txt</c>.</param>
  /// <returns>The strong entity tag, quoted.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="badge" /> is <see langword="null" />.</exception>
  public static string Of(BadgeResponse badge, string extension)
  {
    ArgumentNullException.ThrowIfNull(badge);

    var hash = SHA256.HashData(Encoding.UTF8.GetBytes($"{extension}\n{badge.Label}\n{badge.Message}\n{badge.Color}"));

    return $"\"{Convert.ToHexStringLower(hash.AsSpan(0, 16))}\"";
  }

  /// <summary>Tells whether an <c>If-None-Match</c> header names an entity tag.</summary>
  /// <param name="ifNoneMatch">The values of the header.</param>
  /// <param name="entityTag">The entity tag, quoted.</param>
  /// <returns>Whether the client already holds that representation.</returns>
  public static bool Matches(StringValues ifNoneMatch, string entityTag)
  {
    foreach (var header in ifNoneMatch)
    {
      foreach (var candidate in (header ?? string.Empty).Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
      {
        var tag = candidate.StartsWith("W/", StringComparison.Ordinal) ? candidate[2..] : candidate;

        if (tag == "*" || string.Equals(tag, entityTag, StringComparison.Ordinal))
        {
          return true;
        }
      }
    }

    return false;
  }

  #endregion
}
