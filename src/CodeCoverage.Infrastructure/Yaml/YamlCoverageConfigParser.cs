// ------------------------------------------------------------------------------------------------
// <copyright file="YamlCoverageConfigParser.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Globalization;
using System.Text.RegularExpressions;

using YamlDotNet.Core;
using YamlDotNet.Core.Events;

using CodeCoverage.Domain.Config;
using CodeCoverage.Domain.Contracts.Config;
using CodeCoverage.Domain.Ports;

namespace CodeCoverage.Infrastructure.Yaml;

/// <summary>Reads a <c>codecov.yml</c> file as Codecov does.</summary>
/// <remarks>
///   <para>
///     Codecov loads the file with the <c>safe_load</c> of PyYAML, which follows YAML 1.1: <c>off</c> is false,
///     <c>0x10</c> is 16 and <c>&lt;&lt;</c> merges a map. YamlDotNet only gives the tokens here, and this class
///     types the values the way PyYAML does.
///   </para>
///   <para>
///     The file comes from outside, without authentication on the validation route. The depth is limited so
///     that a deep nesting cannot overflow the stack, an alias that refers to itself is refused, and the
///     number of nodes counts each alias at the size of what it repeats, so that an exponential expansion is
///     refused before anything walks through it.
///   </para>
/// </remarks>
internal sealed partial class YamlCoverageConfigParser : ICoverageConfigParser
{
  #region Fields

  private const int MaximumDepth = 64;

  private const int MaximumNodes = 100_000;

  private const int SexagesimalBase = 60;

  private const string StringTag = "tag:yaml.org,2002:str";

  private const string MapTag = "tag:yaml.org,2002:map";

  private const string SequenceTag = "tag:yaml.org,2002:seq";

  private const string MergeKey = "<<";

  private const string MalformedFile = "The file is not valid YAML.";

  #endregion

  #region Methods

  private static ConfigNode ReadStream(string yaml)
  {
    using var reader = new StringReader(yaml);
    var parser = new Parser(reader);

    parser.Consume<StreamStart>();

    if (parser.TryConsume<StreamEnd>(out _))
    {
      return ConfigNode.Null;
    }

    parser.Consume<DocumentStart>();

    var nodes = 0;
    var root = ReadNode(parser, new Dictionary<string, (ConfigNode Node, int Size)>(StringComparer.Ordinal), ref nodes, 0);

    parser.Consume<DocumentEnd>();

    if (!parser.TryConsume<StreamEnd>(out _))
    {
      throw Refuse(parser.Require<ParsingEvent>(), "The file must hold a single document.");
    }

    return root;
  }

  private static ConfigNode ReadNode(
    IParser parser,
    Dictionary<string, (ConfigNode Node, int Size)> anchors,
    ref int nodes,
    int depth)
  {
    if (parser.TryConsume<AnchorAlias>(out var alias))
    {
      if (!anchors.TryGetValue(alias.Value.Value, out var anchored))
      {
        throw Refuse(alias, $"The alias {alias.Value.Value} is not defined.");
      }

      Count(ref nodes, anchored.Size, alias);

      return anchored.Node;
    }

    var start = parser.Consume<NodeEvent>();

    if (depth > MaximumDepth)
    {
      throw Refuse(start, "The file is nested too deeply.");
    }

    var before = nodes;

    Count(ref nodes, 1, start);

    var node = start switch
    {
      Scalar scalar => ReadScalar(scalar),
      MappingStart => ReadMap(parser, start, anchors, ref nodes, depth),
      SequenceStart => ReadList(parser, start, anchors, ref nodes, depth),
      var _ => throw Refuse(start, "The node cannot be read."),
    };

    if (!start.Anchor.IsEmpty && !anchors.TryAdd(start.Anchor.Value, (node, nodes - before)))
    {
      throw Refuse(start, $"The anchor {start.Anchor.Value} is defined twice.");
    }

    return node;
  }

  private static ConfigNode ReadMap(
    IParser parser,
    NodeEvent start,
    Dictionary<string, (ConfigNode Node, int Size)> anchors,
    ref int nodes,
    int depth)
  {
    RequireTag(start, MapTag);

    var merged = new List<(string Key, ConfigNode Value)>();
    var own = new List<(string Key, ConfigNode Value)>();

    while (!parser.TryConsume<MappingEnd>(out _))
    {
      var keyEvent = parser.Require<ParsingEvent>();
      var key = ReadNode(parser, anchors, ref nodes, depth + 1);
      var value = ReadNode(parser, anchors, ref nodes, depth + 1);

      if (keyEvent is Scalar { Value: MergeKey, Style: ScalarStyle.Plain, Tag.IsEmpty: true })
      {
        merged.AddRange(ToMergedEntries(value, keyEvent));
      }
      else
      {
        own.Add((ToKey(key, keyEvent), value));
      }
    }

    return ConfigNode.FromMap([.. merged, .. own]);
  }

  private static ConfigNode ReadList(
    IParser parser,
    NodeEvent start,
    Dictionary<string, (ConfigNode Node, int Size)> anchors,
    ref int nodes,
    int depth)
  {
    RequireTag(start, SequenceTag);

    var items = new List<ConfigNode>();

    while (!parser.TryConsume<SequenceEnd>(out _))
    {
      items.Add(ReadNode(parser, anchors, ref nodes, depth + 1));
    }

    return ConfigNode.FromList(items);
  }

  private static string ToKey(ConfigNode key, ParsingEvent keyEvent) =>
    keyEvent is Scalar scalar
      ? scalar.Value
      : key.AsText() ?? throw Refuse(keyEvent, "A key must be a text.");

  private static IEnumerable<(string Key, ConfigNode Value)> ToMergedEntries(ConfigNode value, ParsingEvent at) =>
    value.Kind switch
    {
      ConfigNodeKind.Map => value.Entries,
      ConfigNodeKind.List when value.Items.All(static item => item.Kind is ConfigNodeKind.Map) =>
        value.Items.Reverse().SelectMany(static item => item.Entries),
      var _ => throw Refuse(at, "A merge key expects a map or a list of maps."),
    };

  private static void RequireTag(NodeEvent start, string allowed)
  {
    if (!start.Tag.IsEmpty && !start.Tag.IsNonSpecific && start.Tag.Value != allowed)
    {
      throw Refuse(start, $"The tag {start.Tag.Value} is not supported.");
    }
  }

  private static ConfigNode ReadScalar(Scalar scalar)
  {
    if (scalar.Tag.IsNonSpecific || (scalar.Tag.IsEmpty && scalar.Style is ScalarStyle.Plain))
    {
      return Resolve(scalar.Value);
    }

    if (scalar.Tag.IsEmpty || scalar.Tag.Value == StringTag)
    {
      return ConfigNode.FromText(scalar.Value);
    }

    return CoreScalarTag().IsMatch(scalar.Tag.Value)
      ? Resolve(scalar.Value)
      : throw Refuse(scalar, $"The tag {scalar.Tag.Value} is not supported.");
  }

  private static ConfigNode Resolve(string text)
  {
    switch (text)
    {
      case "" or "~" or "null" or "Null" or "NULL":
        return ConfigNode.Null;
      case "yes" or "Yes" or "YES" or "true" or "True" or "TRUE" or "on" or "On" or "ON":
        return ConfigNode.FromBoolean(true);
      case "no" or "No" or "NO" or "false" or "False" or "FALSE" or "off" or "Off" or "OFF":
        return ConfigNode.FromBoolean(false);
    }

    var number = IntegerText().IsMatch(text)
      ? ParseNumber(text, isInteger: true)
      : FloatText().IsMatch(text)
        ? ParseNumber(text, isInteger: false)
        : null;

    return number is { } value ? ConfigNode.FromNumber(value) : ConfigNode.FromText(text);
  }

  private static decimal? ParseNumber(string text, bool isInteger)
  {
    var digits = text.Replace("_", string.Empty, StringComparison.Ordinal);
    var negative = digits[0] == '-';

    if (digits[0] is '-' or '+')
    {
      digits = digits[1..];
    }

    var value = digits.Contains(':', StringComparison.Ordinal)
      ? ParseSexagesimal(digits)
      : isInteger ? ParseInteger(digits) : ParseDecimal(digits);

    return negative ? -value : value;
  }

  private static decimal? ParseInteger(string digits)
  {
    if (digits == "0")
    {
      return 0;
    }

    if (digits.StartsWith("0b", StringComparison.Ordinal))
    {
      return ParseRadix(digits[2..], 2);
    }

    if (digits.StartsWith("0x", StringComparison.Ordinal))
    {
      return ParseRadix(digits[2..], 16);
    }

    return digits[0] == '0' ? ParseRadix(digits[1..], 8) : ParseDecimal(digits);
  }

  private static decimal? ParseRadix(string digits, int radix)
  {
    decimal? value = digits.Length > 0 ? 0 : null;

    foreach (var digit in digits)
    {
      value = Accumulate(value, Convert.ToInt32(digit.ToString(), 16), radix);
    }

    return value;
  }

  private static decimal? ParseSexagesimal(string digits)
  {
    decimal? value = 0;

    foreach (var part in digits.Split(':'))
    {
      value = ParseDecimal(part) is { } number ? Accumulate(value, number, SexagesimalBase) : null;
    }

    return value;
  }

  private static decimal? ParseDecimal(string digits) =>
    decimal.TryParse(digits, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : null;

  private static decimal? Accumulate(decimal? value, decimal digit, int radix) =>
    value is { } current && current <= (decimal.MaxValue - digit) / radix ? (current * radix) + digit : null;

  private static ConfigNode ApplyCompatibilityMoves(ConfigNode root)
  {
    var result = Without(root, "to_string");

    foreach (var key in (string[])["flags", "parsers", "ignore", "fixes"])
    {
      if (result.Find("coverage", key) is { } value)
      {
        result = With(With(result, "coverage", Without(result.Find("coverage")!, key)), key, value);
      }
    }

    if (result.Find("codecov", "notify", "require_ci_to_pass") is { } requireCiToPass)
    {
      var codecov = result.Find("codecov")!;
      var notify = Without(codecov.Find("notify")!, "require_ci_to_pass");

      result = With(result, "codecov", With(With(codecov, "notify", notify), "require_ci_to_pass", requireCiToPass));
    }

    return result;
  }

  private static ConfigNode Without(ConfigNode map, string key) =>
    ConfigNode.FromMap(map.Entries.Where(entry => entry.Key != key));

  private static ConfigNode With(ConfigNode map, string key, ConfigNode value) =>
    ConfigNode.FromMap([.. map.Entries, (key, value)]);

  private static void Count(ref int nodes, int size, ParsingEvent at)
  {
    nodes += size;

    if (nodes > MaximumNodes)
    {
      throw Refuse(at, "The file holds too many values.");
    }
  }

  private static YamlException Refuse(ParsingEvent at, string message) => new(at.Start, at.End, message);

  private static ConfigParseResult Fail(string error) => new(null, error, []);

  [GeneratedRegex("^tag:yaml\\.org,2002:(?:bool|int|float|null|timestamp)\\z", RegexOptions.CultureInvariant)]
  private static partial Regex CoreScalarTag();

  [GeneratedRegex(
    "^[-+]?(?:0b[01_]+|0[0-7_]+|0|[1-9][0-9_]*|0x[0-9a-fA-F_]+|[1-9][0-9_]*(?::[0-5]?[0-9])+)\\z",
    RegexOptions.CultureInvariant)]
  private static partial Regex IntegerText();

  [GeneratedRegex(
    "^(?:[-+]?[0-9][0-9_]*\\.[0-9_]*(?:[eE][-+][0-9]+)?"
    + "|\\.[0-9][0-9_]*(?:[eE][-+][0-9]+)?"
    + "|[-+]?[0-9][0-9_]*(?::[0-5]?[0-9])+\\.[0-9_]*)\\z",
    RegexOptions.CultureInvariant)]
  private static partial Regex FloatText();

  #endregion

  #region Implementation of ICoverageConfigParser

  /// <inheritdoc />
  /// <exception cref="ArgumentNullException"><paramref name="yaml" /> is null.</exception>
  public ConfigParseResult Parse(string yaml)
  {
    ArgumentNullException.ThrowIfNull(yaml);

    ConfigNode root;

    try
    {
      root = ReadStream(yaml);
    }
    catch (YamlException exception)
    {
      return Fail(exception.Message);
    }
    catch (Exception exception) when (exception is InvalidOperationException or ArgumentException)
    {
      // YamlDotNet also throws these on some malformed input, and a configuration file never breaks a build.
      return Fail(MalformedFile);
    }

    if (root.Kind is ConfigNodeKind.Null)
    {
      return new ConfigParseResult(ConfigNode.FromMap(), null, []);
    }

    if (root.Kind is not ConfigNodeKind.Map)
    {
      return Fail("The file must hold a map.");
    }

    var warnings = new List<ConfigWarning>();

    return new ConfigParseResult(SupportedKeys.Review(ApplyCompatibilityMoves(root), warnings), null, warnings);
  }

  #endregion
}
