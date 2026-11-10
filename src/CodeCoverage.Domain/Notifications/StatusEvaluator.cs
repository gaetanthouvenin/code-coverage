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

  /// <summary>Evaluates a rule for a commit: its filters first, then its result.</summary>
  /// <param name="rule">The rule: one of <c>coverage.status</c>, or the status of a flag.</param>
  /// <param name="subject">What the rule is evaluated on.</param>
  /// <param name="facts">The facts of the commit the filters read.</param>
  /// <param name="format">How the status is named and its numbers written.</param>
  /// <returns>The status, or <see langword="null" /> when the rule posts nothing for this commit.</returns>
  /// <exception cref="ArgumentNullException">An argument is <see langword="null" />.</exception>
  /// <remarks>
  ///   A rule whose flags were not uploaded on the commit follows its
  ///   <c>flag_coverage_not_uploaded_behavior</c>: <c>exclude</c> posts nothing, <c>pass</c> sends a success
  ///   that says the coverage was carried forward or is missing, and <c>include</c> evaluates the rule on
  ///   what was carried forward.
  /// </remarks>
  public static CommitStatus? Evaluate(StatusRule rule, StatusSubject subject, StatusRuleFacts facts, StatusFormat format)
  {
    ArgumentNullException.ThrowIfNull(rule);
    ArgumentNullException.ThrowIfNull(facts);

    if (StatusRuleFilter.Skip(rule, facts))
    {
      return null;
    }

    var notUploaded = StatusRuleFilter.NotUploaded(rule, facts);

    if (notUploaded is NotUploadedBehavior.Exclude || Evaluate(rule, subject, format) is not { } status)
    {
      return null;
    }

    return notUploaded is NotUploadedBehavior.Pass
             ? new CommitStatus(status.Context, StatusState.Success, status.Description + StatusRuleFilter.AutoPassedSuffix)
             : status;
  }

  /// <summary>Evaluates a rule, whatever its type, and applies <c>informational</c>.</summary>
  /// <param name="rule">The rule.</param>
  /// <param name="subject">What the rule is evaluated on.</param>
  /// <param name="format">How the status is named and its numbers written.</param>
  /// <returns>The status, or <see langword="null" /> when the rule asks to post nothing (<c>if_not_found: ignore</c>).</returns>
  /// <exception cref="ArgumentNullException">An argument is <see langword="null" />.</exception>
  /// <remarks>An informational rule never blocks: its state is sent as a success, and its description says what was found.</remarks>
  public static CommitStatus? Evaluate(StatusRule rule, StatusSubject subject, StatusFormat format)
  {
    ArgumentNullException.ThrowIfNull(rule);

    var status = rule.Type switch
    {
      StatusType.Project => EvaluateProject(rule, subject, format),
      StatusType.Patch => EvaluatePatch(rule, subject, format),
      var _ => EvaluateChanges(rule, subject, format),
    };

    return status is not null && rule.Informational && status.State is not StatusState.Success
             ? new CommitStatus(status.Context, StatusState.Success, status.Description)
             : status;
  }

  /// <summary>Evaluates a <c>patch</c> rule: the coverage of the lines the diff adds against a target.</summary>
  /// <param name="rule">The rule.</param>
  /// <param name="subject">What the rule is evaluated on.</param>
  /// <param name="format">How the status is named and its numbers written.</param>
  /// <returns>The status, or <see langword="null" /> when the rule asks to post nothing (<c>if_not_found: ignore</c>).</returns>
  /// <exception cref="ArgumentNullException">An argument is <see langword="null" />.</exception>
  /// <exception cref="ArgumentException"><paramref name="rule" /> is not a patch rule.</exception>
  /// <remarks>
  ///   A diff that adds no line the head report can cover does not affect the coverage: the status passes.
  ///   The target <c>auto</c> is the coverage of the base report.
  /// </remarks>
  public static CommitStatus? EvaluatePatch(StatusRule rule, StatusSubject subject, StatusFormat format)
  {
    ArgumentNullException.ThrowIfNull(rule);
    ArgumentNullException.ThrowIfNull(subject);
    ArgumentNullException.ThrowIfNull(format);
    ArgumentException.ThrowIf(rule.Type is not StatusType.Patch, "The rule is not a patch rule.", nameof(rule));

    var context = StatusContext.Build(format.Prefix, rule.Type, rule.Name);
    var patch = subject.Patch.Totals;

    if (patch.Coverage is not { } coverage)
    {
      var from = subject.ForkPointSha ?? subject.ComparedToSha;

      return new CommitStatus(
        context,
        StatusState.Success,
        from is null ? "Coverage not affected" : $"Coverage not affected when comparing {Short(from)}...{Short(subject.HeadSha)}"
      );
    }

    decimal target;
    decimal shownTarget;

    if (rule.Target.Percentage is { } custom)
    {
      target = shownTarget = custom;
    }
    else if (subject.Base?.Totals is { Coverage: { } baseCoverage } @base)
    {
      target = Exact(@base);
      shownTarget = baseCoverage;
    }
    else
    {
      return NotFound(context, rule, NoBaseMessage);
    }

    var exact = Exact(patch);
    var hit = $"{StatusNumber.Text(coverage, format)}% of diff hit";

    if (exact >= target)
    {
      return new CommitStatus(context, StatusState.Success, $"{hit} (target {StatusNumber.Text(shownTarget, format)}%)");
    }

    return exact >= target - rule.Threshold
             ? new CommitStatus(
               context,
               StatusState.Success,
               $"{hit} (within {StatusNumber.Text(rule.Threshold, format)}% threshold of {StatusNumber.Text(shownTarget, format)}%)"
             )
             : new CommitStatus(context, StatusState.Failure, $"{hit} (target {StatusNumber.Text(shownTarget, format)}%)");
  }

  /// <summary>Evaluates a <c>changes</c> rule: no file outside the diff may get more lines missed or partially hit.</summary>
  /// <param name="rule">The rule.</param>
  /// <param name="subject">What the rule is evaluated on.</param>
  /// <param name="format">How the status is named.</param>
  /// <returns>The status.</returns>
  /// <exception cref="ArgumentNullException">An argument is <see langword="null" />.</exception>
  /// <exception cref="ArgumentException"><paramref name="rule" /> is not a changes rule.</exception>
  public static CommitStatus EvaluateChanges(StatusRule rule, StatusSubject subject, StatusFormat format)
  {
    ArgumentNullException.ThrowIfNull(rule);
    ArgumentNullException.ThrowIfNull(subject);
    ArgumentNullException.ThrowIfNull(format);
    ArgumentException.ThrowIf(rule.Type is not StatusType.Changes, "The rule is not a changes rule.", nameof(rule));

    var context = StatusContext.Build(format.Prefix, rule.Type, rule.Name);

    if (subject.Base is null && subject.ForkPointSha is not null && rule.Base is not StatusBase.Parent)
    {
      return new CommitStatus(context, StatusState.Success, "Unable to determine changes, no report found at pull request base");
    }

    var failing = subject.IndirectChanges.Count(static change => change.IsFailing);

    return failing == 0
             ? new CommitStatus(context, StatusState.Success, "No indirect coverage changes found")
             : new CommitStatus(
               context,
               StatusState.Failure,
               $"{failing} {(failing > 1 ? "files have" : "file has")} indirect coverage changes not visible in diff"
             );
  }

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

    if (Exact(head) >= target - rule.Threshold)
    {
      return new CommitStatus(context, StatusState.Success, description);
    }

    // The drop may come from removed code: the behaviour of the rule can still pass the status.
    return RemovedCodeAdjuster.Adjust(rule, subject, format) is { } passed
             ? new CommitStatus(context, StatusState.Success, description + passed)
             : new CommitStatus(context, StatusState.Failure, description);
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