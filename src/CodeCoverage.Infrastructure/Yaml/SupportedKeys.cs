// ------------------------------------------------------------------------------------------------
// <copyright file="SupportedKeys.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Collections.Frozen;
using System.Globalization;

using CodeCoverage.Domain.Config;

namespace CodeCoverage.Infrastructure.Yaml;

/// <summary>The keys of a <c>codecov.yml</c> file and what happens to each of them.</summary>
/// <remarks>
///   A pattern is a dotted path, where <c>*</c> stands for a name chosen by the user and <c>[]</c> for the
///   items of a list. The keys are the ones of <c>user_schema.py</c> of Codecov, sorted as the supported subset
///   of the architecture document says. Codecov refuses a file with a key it does not know: here, a warning.
/// </remarks>
internal static class SupportedKeys
{
  #region Fields

  /// <summary>Neither Codecov nor CodeCoverage knows the key.</summary>
  public const string UnknownKey = "Config.UnknownKey";

  /// <summary>The key is read, but changes nothing yet.</summary>
  public const string WithoutEffectKey = "Config.WithoutEffectKey";

  /// <summary>Codecov knows the key, CodeCoverage does not apply it.</summary>
  public const string IgnoredKey = "Config.IgnoredKey";

  private static readonly string[] NamedStatuses =
  [
    "coverage.status.project.*",
    "coverage.status.patch.*",
    "coverage.status.changes.*",
  ];

  private static readonly string[] FlagRules =
  [
    "flag_management.default_rules",
    "flag_management.individual_flags[]",
  ];

  private static readonly string[] FlagStatuses =
  [
    "flag_management.default_rules.statuses[]",
    "flag_management.individual_flags[].statuses[]",
  ];

  private static readonly string[] Statuses = [.. NamedStatuses, .. FlagStatuses];

  private static readonly FrozenSet<string> Applied = ToSet(
    [
      "codecov",
      "codecov.allow_pseudo_compare",
      "codecov.branch",
      "codecov.disable_default_path_fixes",
      "codecov.max_report_age",
      "codecov.notify",
      "codecov.notify.after_n_builds",
      "codecov.notify.manual_trigger",
      "codecov.notify.notify_error",
      "codecov.strict_yaml_branch",
      "comment",
      "comment.after_n_builds",
      "comment.behavior",
      "comment.branches",
      "comment.files",
      "comment.hide_project_coverage",
      "comment.layout",
      "comment.require_base",
      "comment.require_changes",
      "comment.require_head",
      "comment.show_carryforward_flags",
      "coverage",
      "coverage.precision",
      "coverage.range",
      "coverage.round",
      "coverage.status",
      "coverage.status.changes",
      "coverage.status.default_rules",
      "coverage.status.default_rules.carryforward_behavior",
      "coverage.status.default_rules.flag_coverage_not_uploaded_behavior",
      "coverage.status.patch",
      "coverage.status.project",
      "fixes",
      "flag_management",
      "flag_management.individual_flags",
      "flag_management.individual_flags[].name",
      "flags",
      "flags.*",
      "flags.*.after_n_builds",
      "flags.*.carryforward",
      "flags.*.ignore",
      "flags.*.joined",
      "flags.*.paths",
      "ignore",
      "parsers",
      "parsers.cobertura",
      "parsers.cobertura.handle_missing_conditions",
      "parsers.cobertura.partials_as_hits",
      "parsers.jacoco",
      "parsers.jacoco.partials_as_hits",
      "parsers.lcov",
      "parsers.lcov.partials_as_hits",
      .. NamedStatuses,
      .. FlagRules,
      .. Combine(FlagRules, "after_n_builds", "carryforward", "ignore", "paths", "statuses", "statuses[]"),
      .. Combine(
        Statuses,
        "base",
        "branches",
        "carryforward_behavior",
        "enabled",
        "flag_coverage_not_uploaded_behavior",
        "if_not_found",
        "informational",
        "only_pulls",
        "paths",
        "removed_code_behavior"),
      .. Combine(["coverage.status.project.*", "coverage.status.patch.*"], "target", "threshold"),
      .. Combine(NamedStatuses, "flags"),
      .. Combine(FlagStatuses, "name_prefix", "target", "threshold", "type"),
    ]);

  private static readonly FrozenSet<string> WithoutEffect = ToSet(
    [
      "component_management",
      "github_checks",
      .. Combine(Statuses, "if_ci_failed", "if_no_uploads", "measurement"),
    ]);

  private static readonly FrozenSet<string> WithoutEffectIfTrue = ToSet(
    [
      "codecov.notify.wait_for_ci",
      "codecov.require_ci_to_pass",
    ]);

  private static readonly FrozenSet<string> Ignored = ToSet(
    [
      "codecov.allow_coverage_offsets",
      "codecov.archive",
      "codecov.assume_all_flags",
      "codecov.bot",
      "codecov.ci",
      "codecov.notify.countdown",
      "codecov.notify.delay",
      "codecov.slug",
      "codecov.ui",
      "comment.bundle_change_threshold",
      "comment.flags",
      "comment.hide_comment_details",
      "comment.paths",
      "comment.require_bundle_changes",
      "comment.show_critical_paths",
      "coverage.notify",
      "coverage.status.no_upload_behavior",
      "flags.*.assume",
      "flags.*.carryforward_mode",
      "flags.*.required",
      "parsers.gcov",
      "parsers.go",
      "parsers.javascript",
      "parsers.v1",
      "profiling",
      .. Combine(FlagRules, "carryforward_mode"),
      .. Combine(Statuses, "disable_approx", "skip_if_assumes"),
      .. Combine(["coverage.status.project.*", "coverage.status.patch.*"], "include_changes"),
    ]);

  private static readonly FrozenSet<string> Silent = ToSet(
    [
      "ai_pr_review",
      "beta_groups",
      "bundle_analysis",
      "codecov.token",
      "codecov.url",
      "slack_app",
      "test_analytics",
    ]);

  #endregion

  #region Methods

  /// <summary>Checks the keys of a file and turns its percentages into numbers.</summary>
  /// <param name="root">The content of the file.</param>
  /// <param name="warnings">Receives one warning for each key that is unknown, without effect or ignored.</param>
  /// <returns>
  ///   The same content with each <c>target</c> and <c>threshold</c> written as <c>80%</c> or <c>"80"</c>
  ///   as a number, as <c>PercentSchemaField</c> of Codecov does. A key that is not applied keeps its value.
  /// </returns>
  /// <exception cref="ArgumentNullException"><paramref name="root" /> or <paramref name="warnings" /> is null.</exception>
  public static ConfigNode Review(ConfigNode root, ICollection<ConfigWarning> warnings)
  {
    ArgumentNullException.ThrowIfNull(root);
    ArgumentNullException.ThrowIfNull(warnings);

    return VisitMap(root, string.Empty, string.Empty, warnings);
  }

  private static FrozenSet<string> ToSet(IEnumerable<string> keys) => keys.ToFrozenSet(StringComparer.Ordinal);

  private static IEnumerable<string> Combine(string[] scopes, params string[] keys) =>
    from scope in scopes
    from key in keys
    select string.Concat(scope, ".", key);

  private static string Join(string prefix, string key) => prefix.Length == 0 ? key : string.Concat(prefix, ".", key);

  private static bool IsKnown(string pattern) =>
    Applied.Contains(pattern)
    || WithoutEffect.Contains(pattern)
    || WithoutEffectIfTrue.Contains(pattern)
    || Ignored.Contains(pattern)
    || Silent.Contains(pattern);

  private static ConfigNode Visit(
    ConfigNode node,
    string pattern,
    string path,
    ICollection<ConfigWarning> warnings) =>
    node.Kind switch
    {
      ConfigNodeKind.Map => VisitMap(node, pattern, path, warnings),
      ConfigNodeKind.List when Applied.Contains(pattern + "[]") => VisitList(node, pattern + "[]", path, warnings),
      ConfigNodeKind.Text when IsPercentage(pattern) => ToPercentage(node),
      var _ => node,
    };

  private static ConfigNode VisitMap(
    ConfigNode node,
    string pattern,
    string path,
    ICollection<ConfigWarning> warnings)
  {
    var entries = new List<(string Key, ConfigNode Value)>(node.Entries.Count);

    foreach (var (key, value) in node.Entries)
    {
      var exact = Join(pattern, key);
      var childPattern = IsKnown(exact) ? exact : Join(pattern, "*");
      var childPath = Join(path, key);

      if (WarningFor(childPattern, value) is { } code)
      {
        warnings.Add(new ConfigWarning(code, childPath));
      }

      entries.Add((key, Applied.Contains(childPattern) ? Visit(value, childPattern, childPath, warnings) : value));
    }

    return ConfigNode.FromMap(entries);
  }

  private static ConfigNode VisitList(
    ConfigNode node,
    string itemPattern,
    string path,
    ICollection<ConfigWarning> warnings)
  {
    var items = new List<ConfigNode>(node.Items.Count);

    for (var index = 0; index < node.Items.Count; index++)
    {
      var itemPath = string.Create(CultureInfo.InvariantCulture, $"{path}[{index}]");

      items.Add(Visit(node.Items[index], itemPattern, itemPath, warnings));
    }

    return ConfigNode.FromList(items);
  }

  private static string? WarningFor(string pattern, ConfigNode value)
  {
    if (Applied.Contains(pattern) || Silent.Contains(pattern))
    {
      return null;
    }

    if (WithoutEffectIfTrue.Contains(pattern))
    {
      return value.AsBoolean() is true ? WithoutEffectKey : null;
    }

    if (WithoutEffect.Contains(pattern))
    {
      return WithoutEffectKey;
    }

    return Ignored.Contains(pattern) ? IgnoredKey : UnknownKey;
  }

  private static bool IsPercentage(string pattern) =>
    pattern.EndsWith(".target", StringComparison.Ordinal) || pattern.EndsWith(".threshold", StringComparison.Ordinal);

  private static ConfigNode ToPercentage(ConfigNode node)
  {
    var text = node.AsText()!.TrimEnd('%');

    return text.Length > 0
      && char.IsAsciiDigit(text[0])
      && decimal.TryParse(text, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var number)
        ? ConfigNode.FromNumber(number)
        : node;
  }

  #endregion
}
