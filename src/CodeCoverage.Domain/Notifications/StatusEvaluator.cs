// ------------------------------------------------------------------------------------------------
// <copyright file="StatusEvaluator.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Config;
using CodeCoverage.Domain.Coverage;
using CodeCoverage.Domain.Git;
using CodeCoverage.Domain.Guards;

namespace CodeCoverage.Domain.Notifications;

/// <summary>Evaluates the status rules of a commit (ARCHITECTURE §7.7). Pure.</summary>
/// <remarks>
///   <para>
///     The states and the descriptions are those of Codecov, word for word, so that a branch protection
///     rule and a reader see no difference.
///   </para>
///   <para>
///     A rule is decided on decimals computed from the integers <c>hits / lines</c>, never on a rounded
///     value; the numbers of a description are the coverages as they are stored, with five decimals,
///     rounded to the precision of the configuration.
///   </para>
/// </remarks>
public static class StatusEvaluator
{
  #region Fields

  private const string NoHeadMessage = "No coverage information found on head";

  private const string NoBaseMessage = "No report found to compare against";

  private const string NoBaseCoverageMessage = "No coverage information found on base report";

  private const int ShortShaLength = 7;

  #endregion

  #region Methods

  /// <summary>Evaluates a <c>project</c> rule: the coverage of the head against a target.</summary>
  /// <param name="rule">The rule.</param>
  /// <param name="subject">What the rule is evaluated on.</param>
  /// <param name="format">How the status is named and its numbers written.</param>
  /// <returns>The status, or <see langword="null" /> when the rule asks to post nothing (<c>if_not_found: ignore</c>).</returns>
  /// <exception cref="ArgumentNullException">An argument is <see langword="null" />.</exception>
  /// <exception cref="ArgumentException"><paramref name="rule" /> is not a project rule.</exception>
  public static CommitStatus? EvaluateProject(StatusRule rule, StatusSubject subject, StatusFormat format)
  {
    ArgumentNullException.ThrowIfNull(rule);
    ArgumentNullException.ThrowIfNull(subject);
    ArgumentNullException.ThrowIfNull(format);
    ArgumentException.ThrowIf(rule.Type is not StatusType.Project, "The rule is not a project rule.", nameof(rule));

    var context = StatusContext.Build(format.Prefix, rule.Type, rule.Name);

    if (subject.Head?.Totals is not { Coverage: { } headCoverage } head)
    {
      return NotFound(context, rule, NoHeadMessage);
    }

    // As Codecov: the base is looked for before the target is read, so a numeric target is not compared either
    // when there is no base report, or none with a line to cover.
    if (subject.Base is null)
    {
      return NotFound(context, rule, NoBaseMessage);
    }

    if (subject.Base.Totals is not { Coverage: { } baseCoverage } @base)
    {
      return NotFound(context, rule, NoBaseCoverageMessage);
    }

    decimal target;
    string description;

    if (rule.Target.Percentage is { } custom)
    {
      target = custom;
      description = $"{StatusNumber.Text(headCoverage, format)}% (target {StatusNumber.Text(custom, format)}%)";
    }
    else
    {
      target = Exact(@base);
      description = $"{StatusNumber.Text(headCoverage, format)}% ({StatusNumber.Signed(headCoverage - baseCoverage, format)}%) compared to {Short(subject.ComparedToSha)}";
    }

    var state = Exact(head) >= target - rule.Threshold ? StatusState.Success : StatusState.Failure;

    return new CommitStatus(context, state, description);
  }

  /// <summary>Computes a coverage from its integers, with every digit a decimal holds.</summary>
  /// <param name="totals">The totals, with one line at least.</param>
  /// <returns>The coverage, in percent.</returns>
  internal static decimal Exact(CoverageTotals totals) => 100m * totals.Hits / totals.Lines;

  /// <summary>Writes the first characters of a SHA, as the descriptions of Codecov do.</summary>
  /// <param name="sha">The SHA, or <see langword="null" />.</param>
  /// <returns>Its seven first characters, or an empty text.</returns>
  internal static string Short(GitSha? sha) => sha is null ? string.Empty : sha.Value[..ShortShaLength];

  /// <summary>Builds the status of a rule that has nothing to compare.</summary>
  /// <param name="context">The context of the status.</param>
  /// <param name="rule">The rule, whose <c>if_not_found</c> gives the state.</param>
  /// <param name="message">What is missing.</param>
  /// <returns>The status, or <see langword="null" /> for <c>if_not_found: ignore</c>.</returns>
  internal static CommitStatus? NotFound(string context, StatusRule rule, string message) =>
    rule.IfNotFound switch
    {
      IfNotFoundBehavior.Ignore => null,
      IfNotFoundBehavior.Failure => new CommitStatus(context, StatusState.Failure, message),
      IfNotFoundBehavior.Error => new CommitStatus(context, StatusState.Error, message),
      var _ => new CommitStatus(context, StatusState.Success, message),
    };

  #endregion
}