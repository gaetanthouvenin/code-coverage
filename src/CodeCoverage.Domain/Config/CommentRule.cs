// ------------------------------------------------------------------------------------------------
// <copyright file="CommentRule.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Globalization;

using CodeCoverage.Domain.Paths;

namespace CodeCoverage.Domain.Config;

/// <summary>What the configuration says about the pull request comment (<c>comment</c>).</summary>
public sealed class CommentRule
{
  #region Fields

  private const string Section = "comment";

  private const int DefaultFilesLimit = 10;

  /// <summary>The sections Codecov accepts in a layout and that are not rendered here.</summary>
  private static readonly HashSet<string> UnrenderedSections = new(StringComparer.Ordinal)
  {
    "betaprofiling",
    "changes",
    "components",
    "feedback",
    "file",
    "newheader",
    "reach",
    "suggestions",
    "sunburst",
    "uncovered",
  };

  #endregion

  #region Ctors

  private CommentRule(ConfigNode settings, bool enabled, ICollection<ConfigWarning> warnings)
  {
    var entries = ReadLayoutEntries(settings, warnings);

    Enabled = enabled;
    Layout = ReadLayout(entries, warnings);
    Behavior = ReadBehavior(settings, warnings);
    RequireChanges = ReadRequireChanges(settings, warnings);
    AfterNBuilds = ReadCount(settings, "after_n_builds", 0, warnings);
    FilesLimit = ReadFilesLimit(settings, entries, warnings);
    HideProjectCoverage = ReadBoolean(settings, "hide_project_coverage", warnings);
    ShowCarryforwardFlags = ReadBoolean(settings, "show_carryforward_flags", warnings);
    RequireBase = ReadBoolean(settings, "require_base", warnings);
    RequireHead = ReadBoolean(settings, "require_head", warnings);
    Branches = ConfigPatterns.ReadBranches(settings.Find("branches"), $"{Section}.branches", warnings);
  }

  #endregion

  #region Properties

  /// <summary>Gets a value indicating whether a comment is published at all.</summary>
  public bool Enabled { get; }

  /// <summary>Gets the sections to render, in the order of the configuration.</summary>
  /// <remarks>
  ///   The list stays as written: forcing a <see cref="CommentSection.CondensedHeader" /> when it has no
  ///   header belongs to the rendering, as in <c>get_message_layout</c> of Codecov.
  /// </remarks>
  public IReadOnlyList<CommentSection> Layout { get; }

  /// <summary>Gets how the comment is published.</summary>
  public CommentBehavior Behavior { get; }

  /// <summary>Gets the condition under which a first comment is published.</summary>
  public RequireChangesExpression RequireChanges { get; }

  /// <summary>Gets the number of uploads the comment waits for.</summary>
  public int AfterNBuilds { get; }

  /// <summary>Gets the number of lines of the table of the files.</summary>
  /// <remarks>A limit written in the layout, such as <c>files:5</c>, wins over <c>comment.files</c>.</remarks>
  public int FilesLimit { get; }

  /// <summary>Gets a value indicating whether the project line and the <c>diff</c> table are hidden.</summary>
  public bool HideProjectCoverage { get; }

  /// <summary>Gets a value indicating whether the carried forward flags are listed.</summary>
  public bool ShowCarryforwardFlags { get; }

  /// <summary>Gets a value indicating whether a base report is required (read, warning only).</summary>
  public bool RequireBase { get; }

  /// <summary>Gets a value indicating whether a head report is required (read, warning only).</summary>
  public bool RequireHead { get; }

  /// <summary>Gets the base branches a comment is published for; without any, all of them.</summary>
  public IReadOnlyList<PathPattern> Branches { get; }

  #endregion

  #region Methods

  /// <summary>Reads the rule of an effective configuration.</summary>
  /// <param name="root">The effective configuration.</param>
  /// <param name="warnings">Receives what cannot be applied as written.</param>
  /// <returns>
  ///   The rule. A boolean <c>comment</c> disables the comment, <see langword="true" /> included: Codecov
  ///   only comments when the value is a map (<c>CommentNotifier.is_enabled</c>).
  /// </returns>
  /// <exception cref="ArgumentNullException">
  ///   <paramref name="root" /> or <paramref name="warnings" /> is null.
  /// </exception>
  internal static CommentRule Parse(ConfigNode root, ICollection<ConfigWarning> warnings)
  {
    ArgumentNullException.ThrowIfNull(root);
    ArgumentNullException.ThrowIfNull(warnings);

    var comment = root.Find(Section);

    if (comment?.Kind is ConfigNodeKind.Map)
    {
      return new CommentRule(comment, enabled: true, warnings);
    }

    if (comment?.Kind is not (null or ConfigNodeKind.Null or ConfigNodeKind.Boolean))
    {
      warnings.Add(new ConfigWarning(ConfigWarning.InvalidValue, Section, comment?.AsText()));
    }

    return new CommentRule(ConfigNode.FromMap(), enabled: comment?.Kind is not ConfigNodeKind.Boolean, warnings);
  }

  private static string Key(string option) => $"{Section}.{option}";

  private static ConfigNode BuiltIn(string option) =>
    CoverageConfigDefaults.Root.Find(Section, option) ?? ConfigNode.Null;

  private static CommentSection? ToSection(string name) =>
    name switch
    {
      "header" => CommentSection.Header,
      "condensed_header" => CommentSection.CondensedHeader,
      "diff" => CommentSection.Diff,
      "flag" or "flags" => CommentSection.Flags,
      // Codecov writes the table of the files with missing lines for newfiles and condensed_files: the one of files here.
      "files" or "tree" or "newfiles" or "condensed_files" => CommentSection.Files,
      "footer" or "newfooter" or "condensed_footer" => CommentSection.Footer,
      var _ => null,
    };

  private static string SectionName(string entry) => entry.Split(':')[0];

  private static bool IsShortName(string name) => name is "newfiles" or "condensed_files" or "newfooter" or "condensed_footer";

  private static string[] ReadLayoutEntries(ConfigNode settings, ICollection<ConfigWarning> warnings)
  {
    var node = settings.Find("layout");
    var text = node?.AsText();

    if (text is null)
    {
      WarnIfPresent(node, "layout", warnings);
      text = BuiltIn("layout").AsText()!;
    }

    return text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
  }

  private static List<CommentSection> ReadLayout(string[] entries, ICollection<ConfigWarning> warnings)
  {
    var layout = new List<CommentSection>(entries.Length);

    foreach (var name in entries.Select(SectionName))
    {
      if (ToSection(name) is { } section)
      {
        // A short name of a section the layout already writes, newfiles beside files for instance, adds nothing.
        if (!IsShortName(name) || (!layout.Contains(section) && !entries.Select(SectionName).Any(other => !IsShortName(other) && ToSection(other) == section)))
        {
          layout.Add(section);
        }
      }
      else
      {
        var code = UnrenderedSections.Contains(name) ? ConfigWarning.ClampedValue : ConfigWarning.InvalidValue;

        warnings.Add(new ConfigWarning(code, Key("layout"), name));
      }
    }

    return layout;
  }

  private static int ReadFilesLimit(ConfigNode settings, string[] entries, ICollection<ConfigWarning> warnings)
  {
    int? written = null;

    foreach (var entry in entries.Where(entry => ToSection(SectionName(entry)) is CommentSection.Files && entry.Contains(':')))
    {
      var limit = entry.Split(':')[1];

      if (int.TryParse(limit, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) && value >= 0)
      {
        written ??= value;
      }
      else
      {
        warnings.Add(new ConfigWarning(ConfigWarning.InvalidValue, Key("layout"), entry));
      }
    }

    var configured = ReadCount(settings, "files", DefaultFilesLimit, warnings);

    return written ?? configured;
  }

  private static CommentBehavior ReadBehavior(ConfigNode settings, ICollection<ConfigWarning> warnings)
  {
    var node = settings.Find("behavior");

    switch (node?.AsText())
    {
      case "default":
        return CommentBehavior.Default;

      case "once":
        return CommentBehavior.Once;

      case "new":
        return CommentBehavior.New;

      case "spammy":
        return CommentBehavior.Spammy;

      default:
        WarnIfPresent(node, "behavior", warnings);

        return CommentBehavior.Default;
    }
  }

  private static RequireChangesExpression ReadRequireChanges(ConfigNode settings, ICollection<ConfigWarning> warnings)
  {
    var node = settings.Find("require_changes");

    if (node is not null && RequireChangesExpression.Read(node) is { } expression)
    {
      return expression;
    }

    WarnIfPresent(node, "require_changes", warnings);

    return RequireChangesExpression.None;
  }

  private static bool ReadBoolean(ConfigNode settings, string option, ICollection<ConfigWarning> warnings)
  {
    var node = settings.Find(option);

    if (node?.AsBoolean() is { } value)
    {
      return value;
    }

    WarnIfPresent(node, option, warnings);

    return BuiltIn(option).AsBoolean() is true;
  }

  private static int ReadCount(ConfigNode settings, string option, int fallback, ICollection<ConfigWarning> warnings)
  {
    var node = settings.Find(option);

    if (node?.AsInteger() is >= 0 and var count)
    {
      return count;
    }

    WarnIfPresent(node, option, warnings);

    return fallback;
  }

  private static void WarnIfPresent(ConfigNode? node, string option, ICollection<ConfigWarning> warnings)
  {
    if (node is not null && node.Kind is not ConfigNodeKind.Null)
    {
      warnings.Add(new ConfigWarning(ConfigWarning.InvalidValue, Key(option), node.AsText()));
    }
  }

  #endregion
}
