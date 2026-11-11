// ------------------------------------------------------------------------------------------------
// <copyright file="GitHubPullRequestEvent.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Text.Json;

namespace CodeCoverage.Domain.Contracts.GitHub;

/// <summary>What the body of a <c>pull_request</c> delivery says: which pull request it is about.</summary>
/// <param name="Number">The number of the pull request, or <see langword="null" /> when the body names none.</param>
/// <remarks>
///   Nothing else is read from the body: the state, the head and the base of a pull request always come from the
///   forge, which is the reference.
/// </remarks>
public sealed record GitHubPullRequestEvent(int? Number)
{
  #region Methods

  /// <summary>Reads the body of a delivery.</summary>
  /// <param name="payload">The body, as GitHub sent it.</param>
  /// <returns>What the body says; a body that is malformed names no pull request.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="payload" /> is <see langword="null" />.</exception>
  public static GitHubPullRequestEvent Parse(byte[] payload)
  {
    ArgumentNullException.ThrowIfNull(payload);

    try
    {
      using var document = JsonDocument.Parse(payload);
      var root = document.RootElement;

      if (root.ValueKind is not JsonValueKind.Object)
      {
        return new GitHubPullRequestEvent(Number: null);
      }

      return new GitHubPullRequestEvent(
        ReadNumber(root)
        ?? (root.TryGetProperty("pull_request", out var pull) && pull.ValueKind is JsonValueKind.Object ? ReadNumber(pull) : null)
      );
    }
    catch (JsonException)
    {
      return new GitHubPullRequestEvent(Number: null);
    }
  }

  /// <summary>Reads the positive number of an object.</summary>
  /// <param name="owner">The object.</param>
  /// <returns>The number, or <see langword="null" />.</returns>
  private static int? ReadNumber(JsonElement owner) =>
    owner.TryGetProperty("number", out var number) && number.ValueKind is JsonValueKind.Number && number.TryGetInt32(out var value) && value > 0
      ? value
      : null;

  #endregion
}
