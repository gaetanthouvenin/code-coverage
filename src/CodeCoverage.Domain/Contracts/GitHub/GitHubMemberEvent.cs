// ------------------------------------------------------------------------------------------------
// <copyright file="GitHubMemberEvent.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Globalization;
using System.Text.Json;

namespace CodeCoverage.Domain.Contracts.GitHub;

/// <summary>
///   What the body of an <c>organization</c>, <c>member</c> or <c>membership</c> delivery says about the user it
///   is about.
/// </summary>
/// <param name="UserExternalId">The identifier of the user on GitHub, or <see langword="null" /> when the body names none.</param>
/// <remarks>
///   <c>member</c> (a collaborator of a repository) and <c>membership</c> (a member of a team) name the user in
///   <c>member</c>; <c>organization</c> names it in <c>membership.user</c>. An action that names no user, the
///   rename of an organization for instance, is read with none.
/// </remarks>
public sealed record GitHubMemberEvent(string? UserExternalId)
{
  #region Methods

  /// <summary>Reads the body of a delivery.</summary>
  /// <param name="payload">The body, as GitHub sent it.</param>
  /// <returns>What the body says; a missing or malformed user is left out.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="payload" /> is <see langword="null" />.</exception>
  public static GitHubMemberEvent Parse(byte[] payload)
  {
    ArgumentNullException.ThrowIfNull(payload);

    try
    {
      using var document = JsonDocument.Parse(payload);
      var root = document.RootElement;

      if (root.ValueKind is not JsonValueKind.Object)
      {
        return new GitHubMemberEvent(UserExternalId: null);
      }

      var id = TryGetObject(root, "member", out var member) ? ReadId(member) : null;

      if (id is null && TryGetObject(root, "membership", out var membership) && TryGetObject(membership, "user", out var user))
      {
        id = ReadId(user);
      }

      return new GitHubMemberEvent(id?.ToString(CultureInfo.InvariantCulture));
    }
    catch (JsonException)
    {
      return new GitHubMemberEvent(UserExternalId: null);
    }
  }

  /// <summary>Reads an object property.</summary>
  /// <param name="owner">The object that holds the property.</param>
  /// <param name="name">The name of the property.</param>
  /// <param name="value">The object read.</param>
  /// <returns><see langword="true" /> when the property is an object.</returns>
  private static bool TryGetObject(JsonElement owner, string name, out JsonElement value) =>
    owner.TryGetProperty(name, out value) && value.ValueKind is JsonValueKind.Object;

  /// <summary>Reads the positive numeric identifier of an object.</summary>
  /// <param name="owner">The object.</param>
  /// <returns>The identifier, or <see langword="null" />.</returns>
  private static long? ReadId(JsonElement owner) =>
    owner.TryGetProperty("id", out var id) && id.ValueKind is JsonValueKind.Number && id.TryGetInt64(out var value) && value > 0 ? value : null;

  #endregion
}
