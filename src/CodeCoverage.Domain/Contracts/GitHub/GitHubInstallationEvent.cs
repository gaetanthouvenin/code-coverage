// ------------------------------------------------------------------------------------------------
// <copyright file="GitHubInstallationEvent.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Globalization;
using System.Text.Json;

namespace CodeCoverage.Domain.Contracts.GitHub;

/// <summary>What the body of an <c>installation</c> delivery says about the installation and its account.</summary>
/// <param name="Installation">The installation of the delivery, or <see langword="null" /> when the body names none that can be read.</param>
public sealed record GitHubInstallationEvent(GitHubInstallationInfo? Installation)
{
  #region Methods

  /// <summary>Reads the body of a delivery.</summary>
  /// <param name="payload">The body, as GitHub sent it.</param>
  /// <returns>What the body says; an installation with a missing or malformed part is left out.</returns>
  /// <remarks>
  ///   An account without a login, such as an enterprise, is read with an empty one: the caller decides what
  ///   an account that cannot be an organization is worth.
  /// </remarks>
  /// <exception cref="ArgumentNullException"><paramref name="payload" /> is <see langword="null" />.</exception>
  public static GitHubInstallationEvent Parse(byte[] payload)
  {
    ArgumentNullException.ThrowIfNull(payload);

    try
    {
      using var document = JsonDocument.Parse(payload);
      var root = document.RootElement;

      return new GitHubInstallationEvent(root.ValueKind is JsonValueKind.Object ? ReadInstallation(root) : null);
    }
    catch (JsonException)
    {
      return new GitHubInstallationEvent(Installation: null);
    }
  }

  /// <summary>Reads the installation of the body.</summary>
  /// <param name="root">The body.</param>
  /// <returns>The installation, or <see langword="null" /> when its identifier or the one of its account is missing.</returns>
  private static GitHubInstallationInfo? ReadInstallation(JsonElement root)
  {
    if (!TryGetObject(root, "installation", out var installation)
        || ReadId(installation) is not { } id
        || !TryGetObject(installation, "account", out var account)
        || ReadId(account) is not { } accountId)
    {
      return null;
    }

    var suspended = installation.TryGetProperty("suspended_at", out var suspendedAt) && suspendedAt.ValueKind is JsonValueKind.String;

    return new GitHubInstallationInfo(
      id,
      accountId.ToString(CultureInfo.InvariantCulture),
      ReadText(account, "login") ?? string.Empty,
      GitHubAccountTypes.ToAccountType(ReadText(account, "type") ?? ReadText(installation, "target_type")),
      suspended
    );
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
  /// <returns>The identifier, or <see langword="null" />.</returns>
  private static long? ReadId(JsonElement owner) =>
    owner.TryGetProperty("id", out var id) && id.ValueKind is JsonValueKind.Number && id.TryGetInt64(out var value) && value > 0 ? value : null;

  #endregion
}
