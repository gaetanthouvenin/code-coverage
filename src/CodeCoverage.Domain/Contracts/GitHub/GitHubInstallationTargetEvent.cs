// ------------------------------------------------------------------------------------------------
// <copyright file="GitHubInstallationTargetEvent.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Globalization;
using System.Text.Json;

using CodeCoverage.Domain.Organizations;

namespace CodeCoverage.Domain.Contracts.GitHub;

/// <summary>What the body of an <c>installation_target</c> delivery says about the account of the installation.</summary>
/// <param name="AccountId">The identifier of the account on GitHub, or <see langword="null" /> when the body names none.</param>
/// <param name="Login">The login the account has now, or <see langword="null" /> when the body gives none.</param>
/// <param name="AccountType">The kind of account, or <see langword="null" /> when the body does not say.</param>
/// <remarks>
///   The account is told by its identifier, which a rename never changes: the former login the body also carries
///   (<c>changes.login.from</c>) is not read.
/// </remarks>
public sealed record GitHubInstallationTargetEvent(string? AccountId, string? Login, OrganizationAccountType? AccountType)
{
  #region Methods

  /// <summary>Reads the body of a delivery.</summary>
  /// <param name="payload">The body, as GitHub sent it.</param>
  /// <returns>What the body says; a missing or malformed part is left out.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="payload" /> is <see langword="null" />.</exception>
  public static GitHubInstallationTargetEvent Parse(byte[] payload)
  {
    ArgumentNullException.ThrowIfNull(payload);

    try
    {
      using var document = JsonDocument.Parse(payload);
      var root = document.RootElement;

      if (root.ValueKind is not JsonValueKind.Object
          || !root.TryGetProperty("account", out var account)
          || account.ValueKind is not JsonValueKind.Object)
      {
        return new GitHubInstallationTargetEvent(null, null, null);
      }

      var type = ReadText(account, "type") ?? ReadText(root, "target_type");

      return new GitHubInstallationTargetEvent(
        account.TryGetProperty("id", out var id) && id.ValueKind is JsonValueKind.Number && id.TryGetInt64(out var value) && value > 0
          ? value.ToString(CultureInfo.InvariantCulture)
          : null,
        ReadText(account, "login"),
        type is null ? null : GitHubAccountTypes.ToAccountType(type)
      );
    }
    catch (JsonException)
    {
      return new GitHubInstallationTargetEvent(null, null, null);
    }
  }

  /// <summary>Reads a text property.</summary>
  /// <param name="owner">The object that holds the property.</param>
  /// <param name="name">The name of the property.</param>
  /// <returns>The text, or <see langword="null" />.</returns>
  private static string? ReadText(JsonElement owner, string name) =>
    owner.TryGetProperty(name, out var value) && value.ValueKind is JsonValueKind.String ? value.GetString() : null;

  #endregion
}
