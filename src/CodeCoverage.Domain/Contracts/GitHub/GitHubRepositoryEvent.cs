// ------------------------------------------------------------------------------------------------
// <copyright file="GitHubRepositoryEvent.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Globalization;
using System.Text.Json;

using CodeCoverage.Domain.Contracts.Forges;

namespace CodeCoverage.Domain.Contracts.GitHub;

/// <summary>What the body of a <c>repository</c> or <c>installation_repositories</c> delivery says about repositories.</summary>
/// <param name="Repository">The repository of the delivery, or <see langword="null" /> when the body names none that can be read.</param>
/// <param name="OwnerAccountId">The numeric identifier of the account that owns <paramref name="Repository" /> now, as text.</param>
/// <param name="PreviousOwnerAccountId">
///   The numeric identifier of the account that owned the repository before a transfer, as text, or
///   <see langword="null" />.
/// </param>
/// <param name="RemovedRepositoryIds">The identifiers of the repositories that left the installation, as text.</param>
public sealed record GitHubRepositoryEvent(
  ForgeRepositoryInfo? Repository,
  string? OwnerAccountId,
  string? PreviousOwnerAccountId,
  IReadOnlyList<string> RemovedRepositoryIds)
{
  #region Methods

  /// <summary>Reads the body of a delivery.</summary>
  /// <param name="payload">The body, as GitHub sent it.</param>
  /// <returns>What the body says; every part that is missing or malformed is left empty.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="payload" /> is <see langword="null" />.</exception>
  public static GitHubRepositoryEvent Parse(byte[] payload)
  {
    ArgumentNullException.ThrowIfNull(payload);

    try
    {
      using var document = JsonDocument.Parse(payload);
      var root = document.RootElement;

      if (root.ValueKind is not JsonValueKind.Object)
      {
        return new GitHubRepositoryEvent(null, null, null, []);
      }

      var repository = ReadRepository(root, out var ownerAccountId);

      return new GitHubRepositoryEvent(repository, repository is null ? null : ownerAccountId, ReadPreviousOwner(root), ReadRemoved(root));
    }
    catch (JsonException)
    {
      return new GitHubRepositoryEvent(null, null, null, []);
    }
  }

  /// <summary>Reads the repository of the body.</summary>
  /// <param name="root">The body.</param>
  /// <param name="ownerAccountId">The identifier of the account that owns the repository.</param>
  /// <returns>The repository, or <see langword="null" /> when a part GitHub always sends is missing.</returns>
  private static ForgeRepositoryInfo? ReadRepository(JsonElement root, out string? ownerAccountId)
  {
    ownerAccountId = null;

    if (!TryGetObject(root, "repository", out var repository)
        || ReadId(repository) is not { } id
        || ReadText(repository, "name") is not { Length: > 0 } name
        || !TryGetObject(repository, "owner", out var owner)
        || ReadText(owner, "login") is not { Length: > 0 } login
        || ReadId(owner) is not { } ownerId
        || !repository.TryGetProperty("private", out var isPrivate)
        || isPrivate.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
    {
      return null;
    }

    ownerAccountId = ownerId;

    return new ForgeRepositoryInfo(id, login, string.Empty, name, ReadText(repository, "default_branch") ?? string.Empty, isPrivate.GetBoolean(), ownerId);
  }

  /// <summary>Reads the account that owned the repository before a transfer.</summary>
  /// <param name="root">The body.</param>
  /// <returns>The identifier of the account, or <see langword="null" />.</returns>
  private static string? ReadPreviousOwner(JsonElement root)
  {
    if (!TryGetObject(root, "changes", out var changes)
        || !TryGetObject(changes, "owner", out var owner)
        || !TryGetObject(owner, "from", out var from))
    {
      return null;
    }

    // GitHub names the previous owner under the kind of its account.
    return TryGetObject(from, "organization", out var organization) && ReadId(organization) is { } organizationId
             ? organizationId
             : TryGetObject(from, "user", out var user)
               ? ReadId(user)
               : null;
  }

  /// <summary>Reads the repositories that left the installation.</summary>
  /// <param name="root">The body.</param>
  /// <returns>Their identifiers.</returns>
  private static List<string> ReadRemoved(JsonElement root)
  {
    var removed = new List<string>();

    if (root.TryGetProperty("repositories_removed", out var list) && list.ValueKind is JsonValueKind.Array)
    {
      foreach (var item in list.EnumerateArray())
      {
        if (item.ValueKind is JsonValueKind.Object && ReadId(item) is { } id)
        {
          removed.Add(id);
        }
      }
    }

    return removed;
  }

  /// <summary>Reads an object property.</summary>
  /// <param name="owner">The object that holds the property.</param>
  /// <param name="name">The name of the property.</param>
  /// <param name="value">The object read.</param>
  /// <returns><see langword="true" /> when the property is an object.</returns>
  private static bool TryGetObject(JsonElement owner, string name, out JsonElement value) =>
    owner.TryGetProperty(name, out value) && value.ValueKind is JsonValueKind.Object;

  /// <summary>Reads a text property.</summary>
  /// <param name="owner">The object that holds the property.</param>
  /// <param name="name">The name of the property.</param>
  /// <returns>The text, or <see langword="null" />.</returns>
  private static string? ReadText(JsonElement owner, string name) =>
    owner.TryGetProperty(name, out var value) && value.ValueKind is JsonValueKind.String ? value.GetString() : null;

  /// <summary>Reads the positive numeric identifier of an object.</summary>
  /// <param name="owner">The object.</param>
  /// <returns>The identifier as text, or <see langword="null" />.</returns>
  private static string? ReadId(JsonElement owner) =>
    owner.TryGetProperty("id", out var id) && id.ValueKind is JsonValueKind.Number && id.TryGetInt64(out var value) && value > 0
      ? value.ToString(CultureInfo.InvariantCulture)
      : null;

  #endregion
}
