// ------------------------------------------------------------------------------------------------
// <copyright file="StatusRuleFilter.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Config;
using CodeCoverage.Domain.Paths;

namespace CodeCoverage.Domain.Notifications;

/// <summary>Decides whether a status rule applies to a commit, before it is evaluated (ARCHITECTURE §7.7). Pure.</summary>
public static class StatusRuleFilter
{
  #region Fields

  /// <summary>What <c>flag_coverage_not_uploaded_behavior: pass</c> adds to the description, as Codecov writes it.</summary>
  public const string AutoPassedSuffix = " [Auto passed due to carriedforward or missing coverage]";

  #endregion

  #region Methods

  /// <summary>Tells whether a rule posts nothing for a commit.</summary>
  /// <param name="rule">The rule.</param>
  /// <param name="facts">The facts of the commit.</param>
  /// <returns>
  ///   <see langword="true" /> when the rule is for pull requests only and the commit has none, when its
  ///   <c>branches</c> do not hold the branch of the commit, or when one of its flags has not reached the
  ///   <c>after_n_builds</c> it declares.
  /// </returns>
  /// <exception cref="ArgumentNullException">An argument is <see langword="null" />.</exception>
  /// <remarks>
  ///   The count of a flag includes its carried-forward sessions, as the count of the commit does: a pull
  ///   request that touches one part of a monorepo is still notified.
  /// </remarks>
  public static bool Skip(StatusRule rule, StatusRuleFacts facts)
  {
    ArgumentNullException.ThrowIfNull(rule);
    ArgumentNullException.ThrowIfNull(facts);

    if ((rule.OnlyPulls || rule.Base is StatusBase.Pr) && !facts.HasPullRequest)
    {
      return true;
    }

    // The `match` of Codecov: no negative pattern matches, and a positive one does or there is none.
    if (rule.Branches.Count > 0 && (facts.Branch is null || !new PathPatternSet(rule.Branches).Includes(facts.Branch)))
    {
      return true;
    }

    return rule.Flags.Any(
      flag => facts.AfterNBuildsByFlag.TryGetValue(flag, out var wait) && facts.SessionsByFlag.GetValueOrDefault(flag) < wait
    );
  }

  /// <summary>Tells what a rule does when none of its flags was uploaded on the commit.</summary>
  /// <param name="rule">The rule.</param>
  /// <param name="facts">The facts of the commit.</param>
  /// <returns>
  ///   <see cref="NotUploadedBehavior.Include" /> when the rule has no flag or one of them was uploaded: it
  ///   is then evaluated as it is. Otherwise the behaviour of the rule: <c>exclude</c> posts nothing,
  ///   <c>pass</c> passes the status and says so.
  /// </returns>
  /// <exception cref="ArgumentNullException">An argument is <see langword="null" />.</exception>
  public static NotUploadedBehavior NotUploaded(StatusRule rule, StatusRuleFacts facts)
  {
    ArgumentNullException.ThrowIfNull(rule);
    ArgumentNullException.ThrowIfNull(facts);

    return rule.Flags.Count == 0 || rule.Flags.Any(facts.UploadedFlags.Contains) ? NotUploadedBehavior.Include : rule.NotUploaded;
  }

  /// <summary>Builds the paths the views of a rule keep.</summary>
  /// <param name="rule">The rule.</param>
  /// <param name="config">The effective configuration of the commit.</param>
  /// <returns>The ignored paths of the configuration, then the <c>paths</c> of the rule.</returns>
  /// <exception cref="ArgumentNullException">An argument is <see langword="null" />.</exception>
  public static PathPatternSet PathsOf(StatusRule rule, CoverageConfig config)
  {
    ArgumentNullException.ThrowIfNull(rule);
    ArgumentNullException.ThrowIfNull(config);

    return new PathPatternSet([.. config.BuildPathFilter([]).Patterns, .. rule.Paths]);
  }

  #endregion
}