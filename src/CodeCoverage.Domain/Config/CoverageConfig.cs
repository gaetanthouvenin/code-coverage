// ------------------------------------------------------------------------------------------------
// <copyright file="CoverageConfig.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Globalization;

using CodeCoverage.Domain.Coverage;
using CodeCoverage.Domain.Paths;

namespace CodeCoverage.Domain.Config;

/// <summary>The typed view of an effective configuration.</summary>
/// <remarks>
///   Coverage tooling never breaks a build: a value that cannot be read gives a warning and its built-in
///   default, where Codecov refuses the whole file.
/// </remarks>
public sealed class CoverageConfig
{
  #region Fields

  private const int MaximumWrittenPrecision = 99;

  private readonly List<ConfigWarning> _warnings = [];

  #endregion

  #region Ctors

  private CoverageConfig(ConfigNode root)
  {
    Root = root;
    Precision = ReadPrecision();
    Rounding = ReadRounding();
    Range = ReadRange();
    Ignore = ConfigPatterns.ReadIgnores(root.Find("ignore"), "ignore", _warnings);
    Fixes = ReadFixes();
    DefaultBranch = ReadBranchName();
    StrictYamlBranch = ReadText("codecov", "strict_yaml_branch");
    MaxReportAge = ReadMaxReportAge();
    DisableDefaultPathFixes = ReadBoolean("codecov", "disable_default_path_fixes");
    // Allowed unless the repository forbids it, as in Codecov: the key is not among the built-in defaults.
    AllowPseudoCompare = Root.Find("codecov", "allow_pseudo_compare")?.AsBoolean() is not false;
    RequireCiToPass = ReadBoolean("codecov", "require_ci_to_pass");
    Notify = new NotifyRule(
      ReadCount("codecov", "notify", "after_n_builds"),
      ReadBoolean("codecov", "notify", "wait_for_ci"),
      ReadBoolean("codecov", "notify", "manual_trigger"),
      ReadBoolean("codecov", "notify", "notify_error"));
    Comment = CommentRule.Parse(root, _warnings);
    Cobertura = new ParserRule(
      ReadBoolean("parsers", "cobertura", "partials_as_hits"),
      ReadBoolean("parsers", "cobertura", "handle_missing_conditions"));
    Lcov = new ParserRule(ReadBoolean("parsers", "lcov", "partials_as_hits"), HandleMissingConditions: false);
    Jacoco = new ParserRule(ReadBoolean("parsers", "jacoco", "partials_as_hits"), HandleMissingConditions: false);

    FlagRule.CollectWarnings(root, _warnings);
    _warnings.AddRange(StatusRuleSet.Build(root, []).Warnings);
  }

  #endregion

  #region Properties

  /// <summary>Gets the effective configuration, for the parts that have no view yet.</summary>
  public ConfigNode Root { get; }

  /// <summary>Gets what could not be applied as written.</summary>
  public IReadOnlyList<ConfigWarning> Warnings => _warnings;

  /// <summary>Gets the number of decimals displayed (<c>coverage.precision</c>).</summary>
  public int Precision { get; }

  /// <summary>Gets how a coverage value is rounded (<c>coverage.round</c>).</summary>
  public RoundingMode Rounding { get; }

  /// <summary>Gets the bounds of the color scale (<c>coverage.range</c>).</summary>
  public CoverageRange Range { get; }

  /// <summary>Gets the ignored patterns (<c>ignore</c>), already inverted.</summary>
  public IReadOnlyList<PathPattern> Ignore { get; }

  /// <summary>Gets the path corrections, in the <c>before::after</c> form (<c>fixes</c>).</summary>
  public IReadOnlyList<string> Fixes { get; }

  /// <summary>Gets the branch that replaces the default one (<c>codecov.branch</c>), if any.</summary>
  public string? DefaultBranch { get; }

  /// <summary>Gets the only branch the YAML is read from (<c>codecov.strict_yaml_branch</c>), if any.</summary>
  public string? StrictYamlBranch { get; }

  /// <summary>Gets the age beyond which a report is refused, if a limit applies.</summary>
  public string? MaxReportAge { get; }

  /// <summary>Gets a value indicating whether the default path fixes are disabled.</summary>
  public bool DisableDefaultPathFixes { get; }

  /// <summary>Gets a value indicating whether a pseudo base can be compared: <see langword="true" /> unless the configuration says otherwise.</summary>
  public bool AllowPseudoCompare { get; }

  /// <summary>Gets a value indicating whether the CI must pass (read, without effect).</summary>
  public bool RequireCiToPass { get; }

  /// <summary>Gets when the notifications of a commit are sent (<c>codecov.notify</c>).</summary>
  public NotifyRule Notify { get; }

  /// <summary>Gets what the configuration says about the pull request comment (<c>comment</c>).</summary>
  public CommentRule Comment { get; }

  /// <summary>Gets the options of the Cobertura format.</summary>
  public ParserRule Cobertura { get; }

  /// <summary>Gets the options of the LCOV format.</summary>
  public ParserRule Lcov { get; }

  /// <summary>Gets the options of the JaCoCo format.</summary>
  public ParserRule Jacoco { get; }

  #endregion

  #region Methods

  /// <summary>Reads an effective configuration.</summary>
  /// <param name="root">The result of the merge of its layers.</param>
  /// <returns>The typed view, with its <see cref="Warnings" />.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="root" /> is null.</exception>
  public static CoverageConfig Create(ConfigNode root)
  {
    ArgumentNullException.ThrowIfNull(root);

    return new CoverageConfig(root);
  }

  /// <summary>Resolves the rule of a flag.</summary>
  /// <param name="flag">The name of the flag.</param>
  /// <returns>The rule, which is empty if the configuration does not mention the flag.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="flag" /> is null.</exception>
  public FlagRule ResolveFlag(string flag) => FlagRule.Resolve(Root, flag);

  /// <summary>Gives the flags the configuration declares, in <c>flags</c> and in <c>flag_management.individual_flags</c>.</summary>
  /// <returns>Their names, distinct and in ordinal order.</returns>
  public IReadOnlyList<string> ListDeclaredFlags()
  {
    var legacy = (Root.Find("flags")?.Entries ?? []).Select(static entry => entry.Key);
    var individual = (Root.Find("flag_management", "individual_flags")?.Items ?? []).Select(static item => item.Find("name")?.AsText() ?? string.Empty);

    return [.. legacy.Concat(individual).Where(static flag => flag.Length > 0).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)];
  }

  /// <summary>Tells whether a flag that is not declared is carried forward, by <c>flag_management.default_rules</c>.</summary>
  /// <returns><see langword="true" /> when any flag seen on an ancestor can be carried forward.</returns>
  public bool CarriesForwardByDefault() => Root.Find("flag_management", "default_rules", "carryforward")?.AsBoolean() is true;

  /// <summary>Builds the set that decides which paths an upload keeps.</summary>
  /// <param name="flags">The flags of the upload.</param>
  /// <returns>
  ///   A single set of the inverted <c>ignore</c> entries and of the <c>ignore</c> and <c>paths</c> of
  ///   each flag, as <c>PathFixer.init_from_user_yaml</c> of Codecov builds it.
  /// </returns>
  /// <exception cref="ArgumentNullException"><paramref name="flags" /> is null.</exception>
  public PathPatternSet BuildPathFilter(IEnumerable<string> flags)
  {
    ArgumentNullException.ThrowIfNull(flags);

    var patterns = new List<PathPattern>(Ignore);

    foreach (var flag in flags)
    {
      var rule = ResolveFlag(flag);

      patterns.AddRange(rule.Ignore);
      patterns.AddRange(rule.Paths);
    }

    return new PathPatternSet(patterns);
  }

  /// <summary>Builds the statuses asked for by this configuration.</summary>
  /// <param name="flags">The flags known to the repository.</param>
  /// <returns>The statuses of <c>coverage.status</c>, then those of each flag.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="flags" /> is null.</exception>
  public StatusRuleSet BuildStatusRules(IEnumerable<string> flags) => StatusRuleSet.Build(Root, flags);

  private static string Key(string[] path) => string.Join('.', path);

  private static ConfigNode BuiltIn(string[] path) => CoverageConfigDefaults.Root.Find(path) ?? ConfigNode.Null;

  private static RoundingMode? ToRounding(string? text) =>
    text switch
    {
      "down" => RoundingMode.Down,
      "up" => RoundingMode.Up,
      "nearest" => RoundingMode.Nearest,
      var _ => null,
    };

  private int ReadPrecision()
  {
    string[] path = ["coverage", "precision"];
    var node = Root.Find(path);

    if (node?.AsInteger() is >= 0 and <= MaximumWrittenPrecision and var precision)
    {
      if (precision <= CoverageMath.MaxPrecision)
      {
        return precision;
      }

      _warnings.Add(
        new ConfigWarning(
          ConfigWarning.ClampedValue,
          Key(path),
          precision.ToString(CultureInfo.InvariantCulture)));

      return CoverageMath.MaxPrecision;
    }

    WarnIfPresent(node, path);

    return BuiltIn(path).AsInteger().GetValueOrDefault();
  }

  private RoundingMode ReadRounding()
  {
    string[] path = ["coverage", "round"];
    var node = Root.Find(path);

    if (ToRounding(node?.AsText()) is { } rounding)
    {
      return rounding;
    }

    WarnIfPresent(node, path);

    return ToRounding(BuiltIn(path).AsText()).GetValueOrDefault();
  }

  private CoverageRange ReadRange()
  {
    string[] path = ["coverage", "range"];
    var node = Root.Find(path);

    if (node is not null && CoverageRange.Read(node) is { } range)
    {
      return range;
    }

    WarnIfPresent(node, path);

    return CoverageRange.Read(BuiltIn(path))!;
  }

  private List<string> ReadFixes()
  {
    var node = Root.Find("fixes");

    if (node is null || node.Kind is ConfigNodeKind.Null)
    {
      return [];
    }

    if (node.Kind is not ConfigNodeKind.List)
    {
      _warnings.Add(new ConfigWarning(ConfigWarning.InvalidValue, "fixes", node.AsText()));

      return [];
    }

    var fixes = new List<string>(node.Items.Count);

    for (var index = 0; index < node.Items.Count; index++)
    {
      if (node.Items[index].AsText() is { } text && text.Contains("::", StringComparison.Ordinal))
      {
        fixes.Add(text);
      }
      else
      {
        _warnings.Add(
          new ConfigWarning(
            ConfigWarning.InvalidValue,
            string.Create(CultureInfo.InvariantCulture, $"fixes[{index}]"),
            node.Items[index].AsText()));
      }
    }

    return fixes;
  }

  private string? ReadBranchName()
  {
    var branch = ReadText("codecov", "branch");

    if (branch is null)
    {
      return null;
    }

    foreach (var prefix in (string[])["origin/", "refs/heads/"])
    {
      if (branch.StartsWith(prefix, StringComparison.Ordinal))
      {
        return branch[prefix.Length..];
      }
    }

    return branch;
  }

  private string? ReadMaxReportAge()
  {
    string[] path = ["codecov", "max_report_age"];
    var node = Root.Find(path);

    if (node?.AsText() is { Length: > 0 } text)
    {
      return text;
    }

    if (node?.AsNumber() is { } number and not 0)
    {
      return number.ToString(CultureInfo.InvariantCulture);
    }

    if (node?.AsBoolean() is true)
    {
      WarnIfPresent(node, path);
    }

    return null;
  }

  private string? ReadText(params string[] path)
  {
    var node = Root.Find(path);

    if (node?.AsText() is { } text)
    {
      return text;
    }

    WarnIfPresent(node, path);

    return null;
  }

  private bool ReadBoolean(params string[] path)
  {
    var node = Root.Find(path);

    if (node?.AsBoolean() is { } value)
    {
      return value;
    }

    WarnIfPresent(node, path);

    return BuiltIn(path).AsBoolean() is true;
  }

  private int ReadCount(params string[] path)
  {
    var node = Root.Find(path);

    if (node?.AsInteger() is >= 0 and var count)
    {
      return count;
    }

    WarnIfPresent(node, path);

    return BuiltIn(path).AsInteger().GetValueOrDefault();
  }

  private void WarnIfPresent(ConfigNode? node, string[] path)
  {
    if (node is not null && node.Kind is not ConfigNodeKind.Null)
    {
      _warnings.Add(new ConfigWarning(ConfigWarning.InvalidValue, Key(path), node.AsText()));
    }
  }

  #endregion
}
