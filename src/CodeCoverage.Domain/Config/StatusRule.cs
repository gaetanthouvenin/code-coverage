// ------------------------------------------------------------------------------------------------
// <copyright file="StatusRule.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Paths;

namespace CodeCoverage.Domain.Config;

/// <summary>One commit status asked for by the configuration.</summary>
public sealed class StatusRule
{
  #region Fields

  /// <summary>The name of a status written as <see langword="true" /> or under the <c>default</c> key.</summary>
  public const string DefaultName = "default";

  /// <summary>The key of the behavior when no flag of a status was uploaded.</summary>
  internal const string NotUploadedKey = "flag_coverage_not_uploaded_behavior";

  /// <summary>The former name of <see cref="NotUploadedKey" />.</summary>
  internal const string NotUploadedAlias = "carryforward_behavior";

  private static readonly string[] KeysWithoutEffect = ["if_ci_failed", "if_no_uploads", "measurement"];

  #endregion

  #region Ctors

  private StatusRule(
    StatusType type,
    string name,
    IReadOnlyList<string> flags,
    ConfigNode rule,
    NotUploadedBehavior inherited,
    string key,
    ICollection<ConfigWarning> warnings)
  {
    Type = type;
    Name = name;
    Flags = flags;
    Target = ReadTarget(rule, key, warnings);
    Threshold = ReadValue<decimal>(rule, "threshold", key, warnings, fallback: 0, Target.ReadPercentage);
    Base = ReadValue(rule, "base", key, warnings, StatusBase.Auto, ToBase);
    Paths = ConfigPatterns.ReadPaths(rule.Find("paths"), $"{key}.paths", warnings);
    Branches = ConfigPatterns.ReadBranches(rule.Find("branches"), $"{key}.branches", warnings);
    OnlyPulls = ReadValue(rule, "only_pulls", key, warnings, fallback: false, node => node.AsBoolean());
    Informational = ReadValue(rule, "informational", key, warnings, fallback: false, node => node.AsBoolean());
    IfNotFound = ReadValue(rule, "if_not_found", key, warnings, IfNotFoundBehavior.Success, ToIfNotFound);
    RemovedCode = ReadValue(
      rule,
      "removed_code_behavior",
      key,
      warnings,
      type is StatusType.Project ? RemovedCodeBehavior.AdjustBase : RemovedCodeBehavior.Off,
      ToRemovedCode);
    NotUploaded = ReadNotUploaded(rule, key, warnings, NotUploadedKey, NotUploadedAlias) ?? inherited;

    foreach (var ignored in KeysWithoutEffect)
    {
      if (rule.Find(ignored) is { Kind: not ConfigNodeKind.Null })
      {
        warnings.Add(new ConfigWarning(ConfigWarning.WithoutEffectKey, $"{key}.{ignored}"));
      }
    }
  }

  #endregion

  #region Properties

  /// <summary>Gets what the status measures.</summary>
  public StatusType Type { get; }

  /// <summary>Gets the name of the status: the key of its entry, or the prefixed name of its flag.</summary>
  public string Name { get; }

  /// <summary>Gets the flags the status is limited to; without any, it covers every flag.</summary>
  public IReadOnlyList<string> Flags { get; }

  /// <summary>Gets the expected coverage.</summary>
  public Target Target { get; }

  /// <summary>Gets the loss tolerated under the target, in points of percentage.</summary>
  public decimal Threshold { get; }

  /// <summary>Gets what the head is compared to.</summary>
  public StatusBase Base { get; }

  /// <summary>Gets the patterns the status is limited to; without any, it covers every path.</summary>
  public IReadOnlyList<PathPattern> Paths { get; }

  /// <summary>Gets the patterns of the branches that get the status; without any, every branch does.</summary>
  public IReadOnlyList<PathPattern> Branches { get; }

  /// <summary>Gets a value indicating whether the status is only sent for a pull request.</summary>
  public bool OnlyPulls { get; }

  /// <summary>Gets a value indicating whether the status always passes, its description being kept.</summary>
  public bool Informational { get; }

  /// <summary>Gets the outcome when a report the status needs is missing.</summary>
  public IfNotFoundBehavior IfNotFound { get; }

  /// <summary>Gets how a failing status can still pass; only a <c>project</c> status uses it.</summary>
  public RemovedCodeBehavior RemovedCode { get; }

  /// <summary>Gets what the status does when none of its flags was uploaded on the commit.</summary>
  public NotUploadedBehavior NotUploaded { get; }

  #endregion

  #region Methods

  /// <summary>Reads a status.</summary>
  /// <param name="type">What the status measures.</param>
  /// <param name="name">The name of the status.</param>
  /// <param name="flags">The flags the status is limited to.</param>
  /// <param name="rule">The written values, as a map.</param>
  /// <param name="inherited">The behavior that <c>default_rules</c> gives when the rule has none.</param>
  /// <param name="key">Where the rule is, for the warnings.</param>
  /// <param name="warnings">Receives what cannot be applied as written.</param>
  /// <returns>The status, or <see langword="null" /> if it is disabled.</returns>
  internal static StatusRule? Read(
    StatusType type,
    string name,
    IReadOnlyList<string> flags,
    ConfigNode rule,
    NotUploadedBehavior inherited,
    string key,
    ICollection<ConfigWarning> warnings)
  {
    var status = new StatusRule(type, name, flags, rule, inherited, key, warnings);

    return ReadValue(rule, "enabled", key, warnings, fallback: true, node => node.AsBoolean()) ? status : null;
  }

  /// <summary>Reads the behavior when no flag of a status was uploaded.</summary>
  /// <param name="rule">The map that holds it.</param>
  /// <param name="key">Where the map is, for the warnings.</param>
  /// <param name="warnings">Receives what cannot be applied as written.</param>
  /// <param name="names">The accepted names of the key, the first one winning.</param>
  /// <returns>The behavior, or <see langword="null" /> if none is written or valid.</returns>
  internal static NotUploadedBehavior? ReadNotUploaded(
    ConfigNode rule,
    string key,
    ICollection<ConfigWarning> warnings,
    params string[] names)
  {
    foreach (var name in names)
    {
      if (rule.Find(name) is not { Kind: not ConfigNodeKind.Null } node)
      {
        continue;
      }

      if (ToNotUploaded(node) is { } behavior)
      {
        return behavior;
      }

      warnings.Add(new ConfigWarning(ConfigWarning.InvalidValue, $"{key}.{name}", node.AsText()));
    }

    return null;
  }

  private static Target ReadTarget(ConfigNode rule, string key, ICollection<ConfigWarning> warnings)
  {
    if (rule.Find("target") is not { Kind: not ConfigNodeKind.Null } node)
    {
      return Target.Auto;
    }

    if (Target.Read(node) is { } target)
    {
      return target;
    }

    warnings.Add(new ConfigWarning(ConfigWarning.InvalidValue, $"{key}.target", node.AsText()));

    return Target.Auto;
  }

  private static T ReadValue<T>(
    ConfigNode rule,
    string name,
    string key,
    ICollection<ConfigWarning> warnings,
    T fallback,
    Func<ConfigNode, T?> parse)
    where T : struct
  {
    if (rule.Find(name) is not { Kind: not ConfigNodeKind.Null } node)
    {
      return fallback;
    }

    if (parse(node) is { } value)
    {
      return value;
    }

    warnings.Add(new ConfigWarning(ConfigWarning.InvalidValue, $"{key}.{name}", node.AsText()));

    return fallback;
  }

  private static StatusBase? ToBase(ConfigNode node) =>
    node.AsText() switch
    {
      "auto" => StatusBase.Auto,
      "parent" => StatusBase.Parent,
      "pr" => StatusBase.Pr,
      var _ => null,
    };

  private static IfNotFoundBehavior? ToIfNotFound(ConfigNode node) =>
    node.AsText() switch
    {
      "success" => IfNotFoundBehavior.Success,
      "failure" => IfNotFoundBehavior.Failure,
      "error" => IfNotFoundBehavior.Error,
      "ignore" => IfNotFoundBehavior.Ignore,
      var _ => null,
    };

  // An unquoted off is a boolean for a YAML 1.1 reader, a text for a YAML 1.2 one.
  private static RemovedCodeBehavior? ToRemovedCode(ConfigNode node)
  {
    if (node.AsBoolean() is false)
    {
      return RemovedCodeBehavior.Off;
    }

    return node.AsText() switch
    {
      "adjust_base" => RemovedCodeBehavior.AdjustBase,
      "removals_only" => RemovedCodeBehavior.RemovalsOnly,
      "fully_covered_patch" => RemovedCodeBehavior.FullyCoveredPatch,
      "off" => RemovedCodeBehavior.Off,
      var _ => null,
    };
  }

  private static NotUploadedBehavior? ToNotUploaded(ConfigNode node) =>
    node.AsText() switch
    {
      "include" => NotUploadedBehavior.Include,
      "exclude" => NotUploadedBehavior.Exclude,
      "pass" => NotUploadedBehavior.Pass,
      var _ => null,
    };

  #endregion
}
