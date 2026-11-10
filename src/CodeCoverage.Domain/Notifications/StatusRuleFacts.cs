// ------------------------------------------------------------------------------------------------
// <copyright file="StatusRuleFacts.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Commits;
using CodeCoverage.Domain.Config;

namespace CodeCoverage.Domain.Notifications;

/// <summary>What the filters of a status rule read about a commit.</summary>
/// <param name="HasPullRequest">Whether the commit belongs to a pull request, or to the branch of a merge queue.</param>
/// <param name="Branch">The branch of the commit, or <see langword="null" /> when it is not known.</param>
/// <param name="SessionsByFlag">How many sessions carry each flag, the carried-forward ones included; case-sensitive.</param>
/// <param name="UploadedFlags">The flags one uploaded session at least carries; case-sensitive.</param>
/// <param name="AfterNBuildsByFlag">The number of sessions each flag waits for (<c>after_n_builds</c> of the flag); case-sensitive.</param>
public sealed record StatusRuleFacts(
  bool HasPullRequest,
  string? Branch,
  IReadOnlyDictionary<string, int> SessionsByFlag,
  IReadOnlySet<string> UploadedFlags,
  IReadOnlyDictionary<string, int> AfterNBuildsByFlag)
{
  #region Methods

  /// <summary>Reads the facts of a commit from its sessions and its configuration.</summary>
  /// <param name="hasPullRequest">Whether the commit belongs to a pull request, or to the branch of a merge queue.</param>
  /// <param name="branch">The branch of the commit, or <see langword="null" />.</param>
  /// <param name="sessions">The sessions of the commit.</param>
  /// <param name="config">The effective configuration of the commit.</param>
  /// <param name="rules">The status rules of the commit: the wait of each flag they name is read too.</param>
  /// <returns>The facts.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="sessions" />, <paramref name="config" /> or <paramref name="rules" /> is <see langword="null" />.</exception>
  public static StatusRuleFacts From(bool hasPullRequest, string? branch, IEnumerable<Session> sessions, CoverageConfig config, IEnumerable<StatusRule> rules)
  {
    ArgumentNullException.ThrowIfNull(sessions);
    ArgumentNullException.ThrowIfNull(config);
    ArgumentNullException.ThrowIfNull(rules);

    var counts = new Dictionary<string, int>(StringComparer.Ordinal);
    var uploaded = new HashSet<string>(StringComparer.Ordinal);

    foreach (var session in sessions)
    {
      foreach (var flag in session.Flags.Distinct(StringComparer.Ordinal))
      {
        counts[flag] = counts.GetValueOrDefault(flag) + 1;

        if (session.Kind is SessionKind.Uploaded)
        {
          uploaded.Add(flag);
        }
      }
    }

    var waits = new Dictionary<string, int>(StringComparer.Ordinal);

    // The flags the rules name too: one that is not listed gets flag_management.default_rules, sessions or not.
    foreach (var flag in counts.Keys.Concat(config.ListDeclaredFlags()).Concat(rules.SelectMany(static rule => rule.Flags)).Distinct(StringComparer.Ordinal))
    {
      if (config.ResolveFlag(flag).AfterNBuilds is > 0 and var wait)
      {
        waits[flag] = wait;
      }
    }

    return new StatusRuleFacts(hasPullRequest, branch, counts, uploaded, waits);
  }

  #endregion
}