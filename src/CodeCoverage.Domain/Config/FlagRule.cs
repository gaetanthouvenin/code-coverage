// ------------------------------------------------------------------------------------------------
// <copyright file="FlagRule.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Globalization;

using CodeCoverage.Domain.Paths;

namespace CodeCoverage.Domain.Config;

/// <summary>What the configuration says about one flag.</summary>
public sealed class FlagRule
{
  #region Fields

  private static readonly ConfigNode NoRule = ConfigNode.FromMap();

  #endregion

  #region Ctors

  private FlagRule(ConfigNode rule, bool carryforward, bool joined)
  {
    var ignored = new List<ConfigWarning>();

    Paths = ConfigPatterns.ReadPaths(rule.Find("paths"), string.Empty, ignored);
    Ignore = ConfigPatterns.ReadIgnores(rule.Find("ignore"), string.Empty, ignored);
    AfterNBuilds = ReadCount(rule);
    Statuses = rule.Find("statuses")?.Items ?? [];
    Carryforward = carryforward;
    Joined = joined;
  }

  #endregion

  #region Properties

  /// <summary>Gets the patterns of the flag; without any, the flag covers every path.</summary>
  public IReadOnlyList<PathPattern> Paths { get; }

  /// <summary>Gets the ignored patterns of the flag, already inverted.</summary>
  public IReadOnlyList<PathPattern> Ignore { get; }

  /// <summary>Gets the number of uploads the flag waits for, if it waits for any.</summary>
  public int? AfterNBuilds { get; }

  /// <summary>Gets a value indicating whether the flag is carried forward.</summary>
  public bool Carryforward { get; }

  /// <summary>Gets a value indicating whether the uploads of the flag join the merged report.</summary>
  public bool Joined { get; }

  /// <summary>Gets the statuses of the flag, as written in the configuration.</summary>
  public IReadOnlyList<ConfigNode> Statuses { get; }

  #endregion

  #region Methods

  /// <summary>Resolves the rule of a flag, as <c>get_flag_configuration</c> of Codecov does.</summary>
  /// <param name="root">The effective configuration.</param>
  /// <param name="flag">The name of the flag.</param>
  /// <returns>
  ///   An entry of <c>flags</c>, even empty, used alone. Otherwise <c>default_rules</c>, whose keys are
  ///   replaced by those of the first entry of <c>individual_flags</c> with this name. Otherwise an empty
  ///   rule.
  /// </returns>
  /// <exception cref="ArgumentNullException">
  ///   <paramref name="root" /> or <paramref name="flag" /> is null.
  /// </exception>
  /// <remarks>
  ///   <see cref="Carryforward" /> is the exception, as in <c>flag_has_carryfoward</c>: an empty entry of
  ///   <c>flags</c> does not stop it from reading <c>flag_management</c>.
  /// </remarks>
  public static FlagRule Resolve(ConfigNode root, string flag)
  {
    ArgumentNullException.ThrowIfNull(root);
    ArgumentNullException.ThrowIfNull(flag);

    return new FlagRule(
      FindRule(root, flag),
      ResolveCarryforward(root, flag),
      root.Find("flags", flag, "joined")?.AsBoolean() is not false);
  }

  /// <summary>Reports what cannot be applied in every place that defines a flag rule.</summary>
  /// <param name="root">The effective configuration.</param>
  /// <param name="warnings">Receives the warnings.</param>
  internal static void CollectWarnings(ConfigNode root, ICollection<ConfigWarning> warnings)
  {
    foreach (var (name, rule) in root.Find("flags")?.Entries ?? [])
    {
      Check(rule, $"flags.{name}", warnings);
    }

    if (root.Find("flag_management", "default_rules") is { } general)
    {
      Check(general, "flag_management.default_rules", warnings);
    }

    var individual = IndividualFlags(root);

    for (var index = 0; index < individual.Count; index++)
    {
      Check(
        individual[index],
        string.Create(CultureInfo.InvariantCulture, $"flag_management.individual_flags[{index}]"),
        warnings);
    }
  }

  private static ConfigNode FindRule(ConfigNode root, string flag)
  {
    if (root.Find("flags", flag) is { } legacy)
    {
      return legacy;
    }

    var general = root.Find("flag_management", "default_rules") ?? NoRule;
    var individual = FindIndividualFlags(root, flag).FirstOrDefault();

    return individual is null ? general : ConfigNode.FromMap([.. general.Entries, .. individual.Entries]);
  }

  private static bool ResolveCarryforward(ConfigNode root, string flag)
  {
    if (root.Find("flags", flag) is { Entries.Count: > 0 } legacy)
    {
      return legacy.Find("carryforward")?.AsBoolean() is true;
    }

    foreach (var individual in FindIndividualFlags(root, flag))
    {
      if (individual.Find("carryforward")?.AsBoolean() is { } carryforward)
      {
        return carryforward;
      }
    }

    return root.Find("flag_management", "default_rules", "carryforward")?.AsBoolean() is true;
  }

  private static IEnumerable<ConfigNode> FindIndividualFlags(ConfigNode root, string flag) =>
    IndividualFlags(root).Where(individual => individual.Find("name")?.AsText() == flag);

  private static IReadOnlyList<ConfigNode> IndividualFlags(ConfigNode root) =>
    root.Find("flag_management", "individual_flags")?.Items ?? [];

  private static int? ReadCount(ConfigNode rule) =>
    rule.Find("after_n_builds")?.AsInteger() is >= 0 and var count ? count : null;

  private static void Check(ConfigNode rule, string key, ICollection<ConfigWarning> warnings)
  {
    if (rule.Kind is not ConfigNodeKind.Map)
    {
      warnings.Add(new ConfigWarning(ConfigWarning.InvalidValue, key, rule.AsText()));

      return;
    }

    _ = ConfigPatterns.ReadPaths(rule.Find("paths"), $"{key}.paths", warnings);
    _ = ConfigPatterns.ReadIgnores(rule.Find("ignore"), $"{key}.ignore", warnings);

    if (rule.Find("after_n_builds") is { Kind: not ConfigNodeKind.Null } && ReadCount(rule) is null)
    {
      warnings.Add(new ConfigWarning(ConfigWarning.InvalidValue, $"{key}.after_n_builds"));
    }

    foreach (var option in (string[])["carryforward", "joined"])
    {
      if (rule.Find(option) is { Kind: not ConfigNodeKind.Null } value && value.AsBoolean() is null)
      {
        warnings.Add(new ConfigWarning(ConfigWarning.InvalidValue, $"{key}.{option}", value.AsText()));
      }
    }
  }

  #endregion
}
