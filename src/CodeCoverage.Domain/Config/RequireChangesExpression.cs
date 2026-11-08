// ------------------------------------------------------------------------------------------------
// <copyright file="RequireChangesExpression.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Domain.Config;

/// <summary>The condition under which a first comment is published (<c>comment.require_changes</c>).</summary>
/// <remarks>
///   The expression is the one of <c>CoverageCommentRequirementSchemaField</c> of Codecov: a list of
///   <c>or</c> groups joined by <c>and</c>, every group having to hold. <c>a and b or c</c> therefore
///   means <c>a and (b or c)</c>.
/// </remarks>
public sealed class RequireChangesExpression
{
  #region Fields

  /// <summary>
  ///   The value of <c>Decimal(0.01)</c> in Codecov. That double is slightly above a hundredth, so a gap
  ///   of exactly 0.01 does not reach it.
  /// </summary>
  private const decimal Hundredth = 0.0100000000000000002081668171m;

  private const decimal FullCoverage = 100;

  private readonly List<List<Func<RequireChangesFacts, bool>>> _groups;

  #endregion

  #region Ctors

  private RequireChangesExpression(List<List<Func<RequireChangesFacts, bool>>> groups) => _groups = groups;

  #endregion

  #region Properties

  /// <summary>Gets the expression that always holds (<c>false</c>).</summary>
  public static RequireChangesExpression None { get; } = new([]);

  #endregion

  #region Methods

  /// <summary>Reads an expression.</summary>
  /// <param name="node">
  ///   <see langword="true" />, <see langword="false" /> or a text that combines <c>any_change</c>,
  ///   <c>coverage_drop</c> and <c>uncovered_patch</c> with <c>and</c> and <c>or</c>, in any case.
  /// </param>
  /// <returns>The expression, or <see langword="null" /> if the value is malformed.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="node" /> is null.</exception>
  public static RequireChangesExpression? Read(ConfigNode node)
  {
    ArgumentNullException.ThrowIfNull(node);

    if (node.AsBoolean() is { } required)
    {
      return required ? new RequireChangesExpression([[AnyChange]]) : None;
    }

    return node.AsText() is { } text ? Parse(text) : null;
  }

  /// <summary>Tells whether the expression holds for a comparison.</summary>
  /// <param name="facts">What the comparison says.</param>
  /// <returns><see langword="true" /> if every group has a condition that holds.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="facts" /> is null.</exception>
  public bool Evaluate(RequireChangesFacts facts)
  {
    ArgumentNullException.ThrowIfNull(facts);

    return _groups.TrueForAll(group => group.Exists(condition => condition(facts)));
  }

  private static RequireChangesExpression? Parse(string text)
  {
    var words = text.ToLowerInvariant().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);

    if (words.Length % 2 == 0)
    {
      return null;
    }

    List<List<Func<RequireChangesFacts, bool>>> groups = [[]];

    for (var index = 0; index < words.Length; index++)
    {
      if (index % 2 == 0)
      {
        if (ToCondition(words[index]) is not { } condition)
        {
          return null;
        }

        groups[^1].Add(condition);
      }
      else if (words[index] == "and")
      {
        groups.Add([]);
      }
      else if (words[index] != "or")
      {
        return null;
      }
    }

    return new RequireChangesExpression(groups);
  }

  private static Func<RequireChangesFacts, bool>? ToCondition(string word) =>
    word switch
    {
      "any_change" => AnyChange,
      "coverage_drop" => CoverageDrop,
      "uncovered_patch" => UncoveredPatch,
      var _ => null,
    };

  private static bool AnyChange(RequireChangesFacts facts) => facts.HasIndirectChanges || facts.PatchLines > 0;

  private static bool CoverageDrop(RequireChangesFacts facts)
  {
    if (facts.HeadCoverage is not { } head || facts.BaseCoverage is not { } origin)
    {
      return true;
    }

    var change = head - origin;

    return change < 0 && Math.Abs(change) >= facts.ProjectThreshold + Hundredth;
  }

  private static bool UncoveredPatch(RequireChangesFacts facts) =>
    facts.PatchLines > 0 && facts.PatchCoverage is { } patch && Math.Abs(patch - FullCoverage) >= Hundredth;

  #endregion
}
