// ------------------------------------------------------------------------------------------------
// <copyright file="StatusRuleSet.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Globalization;

namespace CodeCoverage.Domain.Config;

/// <summary>The commit statuses a configuration asks for.</summary>
public sealed class StatusRuleSet
{
  #region Fields

  private const int MaximumFlagNameLength = 1024;

  private const string FlagManagement = "flag_management";

  private const string StatusesKey = "statuses";

  private static readonly ConfigNode NoRule = ConfigNode.FromMap();

  #endregion

  #region Ctors

  private StatusRuleSet(IReadOnlyList<StatusRule> rules, IReadOnlyList<ConfigWarning> warnings)
  {
    Rules = rules;
    Warnings = warnings;
  }

  #endregion

  #region Properties

  /// <summary>Gets the enabled statuses: those of <c>coverage.status</c>, then those of each flag.</summary>
  public IReadOnlyList<StatusRule> Rules { get; }

  /// <summary>
  ///   Gets what could not be applied as written, wherever it is written, whichever flags are given.
  /// </summary>
  public IReadOnlyList<ConfigWarning> Warnings { get; }

  #endregion

  #region Methods

  /// <summary>Builds the statuses of a configuration, as <c>get_statuses</c> of Codecov does.</summary>
  /// <param name="root">The effective configuration.</param>
  /// <param name="flags">The flags known to the repository, which are the only ones to get a status.</param>
  /// <returns>The statuses, with their <see cref="Warnings" />.</returns>
  /// <exception cref="ArgumentNullException">
  ///   <paramref name="root" /> or <paramref name="flags" /> is null.
  /// </exception>
  public static StatusRuleSet Build(ConfigNode root, IEnumerable<string> flags)
  {
    ArgumentNullException.ThrowIfNull(root);
    ArgumentNullException.ThrowIfNull(flags);

    var warnings = new List<ConfigWarning>();
    var rules = new List<StatusRule>();
    var status = ReadStatus(root, warnings);
    var inherited = ReadDefaultRules(status, warnings);

    foreach (var (name, section) in status.Entries)
    {
      if (ToType(name) is { } type)
      {
        AddSection(type, section, $"coverage.status.{name}", inherited, rules, warnings);
      }
    }

    CheckFlagStatuses(root, inherited, warnings);

    foreach (var flag in flags.Distinct(StringComparer.Ordinal))
    {
      AddFlagRules(root, flag, inherited, rules);
    }

    return new StatusRuleSet(rules, warnings);
  }

  private static StatusType? ToType(string? name) =>
    name switch
    {
      "project" => StatusType.Project,
      "patch" => StatusType.Patch,
      "changes" => StatusType.Changes,
      var _ => null,
    };

  // A status section can only be a map. Codecov stops on anything else that is true, as it calls items() on
  // it; the built-in statuses apply here, so that a key written wrong does not silence every status.
  private static ConfigNode ReadStatus(ConfigNode root, ICollection<ConfigWarning> warnings)
  {
    if (root.Find("coverage", "status") is not { } status
      || status.Kind is ConfigNodeKind.Null
      || status.AsBoolean() is false)
    {
      return NoRule;
    }

    if (status.Kind is ConfigNodeKind.Map)
    {
      return status;
    }

    warnings.Add(new ConfigWarning(ConfigWarning.InvalidValue, "coverage.status", status.AsText()));

    return CoverageConfigDefaults.Root.Find("coverage", "status") ?? NoRule;
  }

  // The alias comes first: the built-in layer always defines the current name, which would hide it.
  private static NotUploadedBehavior ReadDefaultRules(ConfigNode status, ICollection<ConfigWarning> warnings)
  {
    const string Key = "coverage.status.default_rules";

    if (status.Find("default_rules") is not { Kind: not ConfigNodeKind.Null } rules)
    {
      return NotUploadedBehavior.Include;
    }

    if (rules.Kind is not ConfigNodeKind.Map)
    {
      warnings.Add(new ConfigWarning(ConfigWarning.InvalidValue, Key, rules.AsText()));

      return NotUploadedBehavior.Include;
    }

    return StatusRule.ReadNotUploaded(rules, Key, warnings, StatusRule.NotUploadedAlias, StatusRule.NotUploadedKey)
      ?? NotUploadedBehavior.Include;
  }

  private static void AddSection(
    StatusType type,
    ConfigNode section,
    string key,
    NotUploadedBehavior inherited,
    List<StatusRule> rules,
    List<ConfigWarning> warnings)
  {
    if (section.AsBoolean() is true)
    {
      AddEntry(type, StatusRule.DefaultName, NoRule, $"{key}.{StatusRule.DefaultName}", inherited, rules, warnings);

      return;
    }

    if (section.Kind is ConfigNodeKind.Map)
    {
      foreach (var (name, entry) in section.Entries)
      {
        if (IsValidName(name))
        {
          AddEntry(type, name, entry, $"{key}.{name}", inherited, rules, warnings);
        }
        else
        {
          warnings.Add(new ConfigWarning(ConfigWarning.InvalidValue, $"{key}.{name}", name));
        }
      }

      return;
    }

    if (section.Kind is not (ConfigNodeKind.Boolean or ConfigNodeKind.Null))
    {
      warnings.Add(new ConfigWarning(ConfigWarning.InvalidValue, key, section.AsText()));
    }
  }

  private static void AddEntry(
    StatusType type,
    string name,
    ConfigNode entry,
    string key,
    NotUploadedBehavior inherited,
    List<StatusRule> rules,
    List<ConfigWarning> warnings)
  {
    if (entry.Kind is ConfigNodeKind.Null || entry.AsBoolean() is false)
    {
      return;
    }

    if (entry.Kind is not (ConfigNodeKind.Map or ConfigNodeKind.Boolean))
    {
      warnings.Add(new ConfigWarning(ConfigWarning.InvalidValue, key, entry.AsText()));

      return;
    }

    var written = entry.Kind is ConfigNodeKind.Map ? entry : NoRule;
    var flags = ReadFlagNames(written.Find("flags"), $"{key}.flags", warnings);

    if (StatusRule.Read(type, name, flags, written, inherited, key, warnings) is { } rule)
    {
      rules.Add(rule);
    }
  }

  private static List<string> ReadFlagNames(ConfigNode? list, string key, ICollection<ConfigWarning> warnings)
  {
    if (list is null || list.Kind is ConfigNodeKind.Null)
    {
      return [];
    }

    if (list.Kind is not ConfigNodeKind.List)
    {
      warnings.Add(new ConfigWarning(ConfigWarning.InvalidValue, key, list.AsText()));

      return [];
    }

    var names = new List<string>(list.Items.Count);

    for (var index = 0; index < list.Items.Count; index++)
    {
      if (list.Items[index].AsText() is { } name && IsValidFlagName(name))
      {
        names.Add(name);
      }
      else
      {
        warnings.Add(
          new ConfigWarning(
            ConfigWarning.InvalidValue,
            string.Create(CultureInfo.InvariantCulture, $"{key}[{index}]"),
            list.Items[index].AsText()));
      }
    }

    return names;
  }

  // Only flag_management can hold statuses: Codecov refuses them in the legacy flags section.
  private static void CheckFlagStatuses(
    ConfigNode root,
    NotUploadedBehavior inherited,
    ICollection<ConfigWarning> warnings)
  {
    CheckStatuses(
      root.Find(FlagManagement, "default_rules", StatusesKey),
      $"{FlagManagement}.default_rules.{StatusesKey}",
      inherited,
      warnings);

    var individual = root.Find(FlagManagement, "individual_flags")?.Items ?? [];

    for (var index = 0; index < individual.Count; index++)
    {
      CheckStatuses(
        individual[index].Find(StatusesKey),
        string.Create(CultureInfo.InvariantCulture, $"{FlagManagement}.individual_flags[{index}].{StatusesKey}"),
        inherited,
        warnings);
    }
  }

  private static void CheckStatuses(
    ConfigNode? statuses,
    string key,
    NotUploadedBehavior inherited,
    ICollection<ConfigWarning> warnings)
  {
    if (statuses is null || statuses.Kind is ConfigNodeKind.Null)
    {
      return;
    }

    if (statuses.Kind is not ConfigNodeKind.List)
    {
      warnings.Add(new ConfigWarning(ConfigWarning.InvalidValue, key, statuses.AsText()));

      return;
    }

    for (var index = 0; index < statuses.Items.Count; index++)
    {
      _ = ReadFlagStatus(
        string.Empty,
        statuses.Items[index],
        string.Create(CultureInfo.InvariantCulture, $"{key}[{index}]"),
        inherited,
        warnings);
    }
  }

  // The warnings were reported where the statuses are written, not once per flag that resolves to them.
  private static void AddFlagRules(ConfigNode root, string flag, NotUploadedBehavior inherited, List<StatusRule> rules)
  {
    var ignored = new List<ConfigWarning>();

    foreach (var status in FlagRule.Resolve(root, flag).Statuses)
    {
      if (ReadFlagStatus(flag, status, string.Empty, inherited, ignored) is { } rule)
      {
        rules.Add(rule);
      }
    }
  }

  private static StatusRule? ReadFlagStatus(
    string flag,
    ConfigNode status,
    string key,
    NotUploadedBehavior inherited,
    ICollection<ConfigWarning> warnings)
  {
    if (status.Kind is not ConfigNodeKind.Map)
    {
      warnings.Add(new ConfigWarning(ConfigWarning.InvalidValue, key, status.AsText()));

      return null;
    }

    if (ToType(status.Find("type")?.AsText()) is not { } type)
    {
      warnings.Add(new ConfigWarning(ConfigWarning.InvalidValue, $"{key}.type", status.Find("type")?.AsText()));

      return null;
    }

    var prefix = ReadNamePrefix(status, key, warnings);

    return StatusRule.Read(type, $"{prefix}{flag}", [flag], status, inherited, key, warnings);
  }

  private static string ReadNamePrefix(ConfigNode status, string key, ICollection<ConfigWarning> warnings)
  {
    if (status.Find("name_prefix") is not { Kind: not ConfigNodeKind.Null } node)
    {
      return string.Empty;
    }

    if (node.AsText() is { } prefix && IsValidName(prefix))
    {
      return prefix;
    }

    warnings.Add(new ConfigWarning(ConfigWarning.InvalidValue, $"{key}.name_prefix", node.AsText()));

    return string.Empty;
  }

  private static bool IsValidName(string name) =>
    name.Length > 0 && name.All(character => char.IsLetterOrDigit(character) || character is '_' or '-' or '.');

  private static bool IsValidFlagName(string name) =>
    name.Length is > 0 and <= MaximumFlagNameLength && !name.Contains('\'') && !name.Contains('"');

  #endregion
}
