// ------------------------------------------------------------------------------------------------
// <copyright file="GitHubPushEvent.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Text.Json;

using CodeCoverage.Domain.Config;
using CodeCoverage.Domain.Git;

namespace CodeCoverage.Domain.Contracts.GitHub;

/// <summary>What the body of a <c>push</c> delivery says about a branch.</summary>
/// <param name="Branch">The name of the branch, or <see langword="null" /> when the push is not about a branch, a tag for instance.</param>
/// <param name="Head">The commit the branch points to after the push, or <see langword="null" /> when the branch was deleted.</param>
/// <param name="Deleted">Whether the push deleted the branch.</param>
/// <param name="TouchesConfig">
///   Whether a commit of the push adds or modifies a configuration file, or the push was forced: a forced push may take
///   the branch back to a commit whose file differs, and lists none of the commits it removed.
/// </param>
/// <param name="Before">
///   The commit the branch pointed to before the push, or <see langword="null" /> when the push created the branch.
/// </param>
/// <remarks>
///   The commits listed by the body are never read as a history: GitHub cuts the list, and its order tells
///   nothing about the parents. They only tell that a configuration file may have changed, which is then read
///   from the forge.
/// </remarks>
public sealed record GitHubPushEvent(string? Branch, GitSha? Head, bool Deleted, bool TouchesConfig, GitSha? Before = null)
{
  #region Fields

  /// <summary>The start of the reference of a branch.</summary>
  private const string BranchPrefix = "refs/heads/";

  #endregion

  #region Methods

  /// <summary>Reads the body of a delivery.</summary>
  /// <param name="payload">The body, as GitHub sent it.</param>
  /// <returns>What the body says; a body that is not about a branch, or is malformed, names no branch.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="payload" /> is <see langword="null" />.</exception>
  public static GitHubPushEvent Parse(byte[] payload)
  {
    ArgumentNullException.ThrowIfNull(payload);

    try
    {
      using var document = JsonDocument.Parse(payload);
      var root = document.RootElement;

      if (root.ValueKind is not JsonValueKind.Object
          || ReadText(root, "ref") is not { } reference
          || !reference.StartsWith(BranchPrefix, StringComparison.Ordinal)
          || reference.Length == BranchPrefix.Length)
      {
        return Nothing();
      }

      var branch = reference[BranchPrefix.Length..];
      var deleted = root.TryGetProperty("deleted", out var flag) && flag.ValueKind is JsonValueKind.True;

      if (deleted)
      {
        return new GitHubPushEvent(branch, null, true, false);
      }

      // A push that deletes nothing always names a commit: without one, the body tells nothing that can be applied.
      if (!GitSha.TryParse(ReadText(root, "after"), out var head) || IsZero(head))
      {
        return Nothing();
      }

      var forced = root.TryGetProperty("forced", out var force) && force.ValueKind is JsonValueKind.True;
      var before = GitSha.TryParse(ReadText(root, "before"), out var previous) && !IsZero(previous) ? previous : null;

      return new GitHubPushEvent(branch, head, false, forced || TouchesConfiguration(root), before);
    }
    catch (JsonException)
    {
      return Nothing();
    }
  }

  /// <summary>Builds the reading of a body that is not about a branch.</summary>
  /// <returns>A push that names no branch.</returns>
  private static GitHubPushEvent Nothing() => new(null, null, false, false);

  /// <summary>Tells whether a SHA is the one GitHub writes for a commit that does not exist.</summary>
  /// <param name="sha">The SHA.</param>
  /// <returns><see langword="true" /> when the SHA only holds zeros.</returns>
  private static bool IsZero(GitSha sha) => sha.Value.All(static character => character is '0');

  /// <summary>Tells whether a commit of the push adds or modifies a configuration file.</summary>
  /// <param name="root">The body.</param>
  /// <returns><see langword="true" /> when one of the listed commits, or the head commit, does.</returns>
  private static bool TouchesConfiguration(JsonElement root)
  {
    if (root.TryGetProperty("head_commit", out var head) && TouchesConfiguration(head, "added", "modified"))
    {
      return true;
    }

    if (!root.TryGetProperty("commits", out var commits) || commits.ValueKind is not JsonValueKind.Array)
    {
      return false;
    }

    foreach (var commit in commits.EnumerateArray())
    {
      if (TouchesConfiguration(commit, "added", "modified"))
      {
        return true;
      }
    }

    return false;
  }

  /// <summary>Tells whether one commit lists a configuration file among its changes.</summary>
  /// <param name="commit">The commit, as the body describes it.</param>
  /// <param name="lists">The names of the lists of paths to read.</param>
  /// <returns><see langword="true" /> when a listed path is the one of a configuration file.</returns>
  private static bool TouchesConfiguration(JsonElement commit, params ReadOnlySpan<string> lists)
  {
    if (commit.ValueKind is not JsonValueKind.Object)
    {
      return false;
    }

    foreach (var list in lists)
    {
      if (!commit.TryGetProperty(list, out var paths) || paths.ValueKind is not JsonValueKind.Array)
      {
        continue;
      }

      foreach (var path in paths.EnumerateArray())
      {
        if (path.ValueKind is JsonValueKind.String && ConfigFileLocations.IsConfigPath(path.GetString()))
        {
          return true;
        }
      }
    }

    return false;
  }

  /// <summary>Reads a text property.</summary>
  /// <param name="owner">The object that holds the property.</param>
  /// <param name="name">The name of the property.</param>
  /// <returns>The text, or <see langword="null" />.</returns>
  private static string? ReadText(JsonElement owner, string name) =>
    owner.TryGetProperty(name, out var value) && value.ValueKind is JsonValueKind.String ? value.GetString() : null;

  #endregion
}
