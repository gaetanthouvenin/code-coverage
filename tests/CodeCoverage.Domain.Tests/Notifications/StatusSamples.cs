// ------------------------------------------------------------------------------------------------
// <copyright file="StatusSamples.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Comparisons;
using CodeCoverage.Domain.Config;
using CodeCoverage.Domain.Coverage;
using CodeCoverage.Domain.Diffs;
using CodeCoverage.Domain.Git;
using CodeCoverage.Domain.Notifications;
using CodeCoverage.Domain.Paths;

namespace CodeCoverage.Domain.Tests.Notifications;

// What the status tests share: rules read from a configuration, views with given totals, a subject.
internal static class StatusSamples
{
  #region Fields

  public static readonly GitSha HeadSha = GitSha.Parse("aaaaaaa1" + new string('0', 32));

  public static readonly GitSha BaseSha = GitSha.Parse("bbbbbbb2" + new string('0', 32));

  public static readonly GitSha ForkSha = GitSha.Parse("ccccccc3" + new string('0', 32));

  public static readonly StatusFormat Format = new("coverage", 2, RoundingMode.Down);

  #endregion

  #region Methods

  // The rule `default` of a type, read as the configuration reads it, with the given keys.
  public static StatusRule Rule(StatusType type, params (string Key, ConfigNode Value)[] entries) => Named(type, StatusRule.DefaultName, entries);

  public static StatusRule Named(StatusType type, string name, params (string Key, ConfigNode Value)[] entries)
  {
    var key = type.ToString().ToLowerInvariant();
    var root = ConfigNode.FromMap(("coverage", ConfigNode.FromMap(("status", ConfigNode.FromMap((key, ConfigNode.FromMap((name, ConfigNode.FromMap(entries)))))))));

    return StatusRuleSet.Build(root, []).Rules.Single(rule => rule.Type == type && rule.Name == name);
  }

  public static ConfigNode Text(string value) => ConfigNode.FromText(value);

  public static ConfigNode Number(decimal value) => ConfigNode.FromNumber(value);

  public static ConfigNode Bool(bool value) => ConfigNode.FromBoolean(value);

  // A view of one file with the given numbers of lines hit, missed and partially hit.
  public static ReportView View(int hits, int misses, int partials = 0) => ViewOf(File("src/a.cs", hits, misses, partials));

  public static ReportView ViewOf(params FileCoverage[] files) =>
    ReportView.Build([new SessionReport([], CoverageReport.Create(files))], [], new PathPatternSet([]));

  public static FileCoverage File(string path, int hits, int misses, int partials = 0)
  {
    var lines = new List<LineCoverage>();

    for (var i = 0; i < hits; i++)
    {
      lines.Add(new LineCoverage(lines.Count + 1, 1, 0, 0, false, null));
    }

    for (var i = 0; i < misses; i++)
    {
      lines.Add(new LineCoverage(lines.Count + 1, 0, 0, 0, false, null));
    }

    for (var i = 0; i < partials; i++)
    {
      lines.Add(new LineCoverage(lines.Count + 1, 1, 1, 2, false, null));
    }

    return FileCoverage.Create(path, lines);
  }

  public static PatchCoverage Patch(int hits, int misses, int partials = 0) =>
    new(new CoverageTotals(hits, misses, partials, 0, 0), [new PatchFileCoverage("src/a.cs", new CoverageTotals(hits, misses, partials, 0, 0), false)]);

  public static DiffSet Diff(params FileDiff[] files) => new(files, Truncated: false, DiffSource.Compare);

  public static StatusSubject Subject(
    ReportView? head,
    ReportView? @base,
    PatchCoverage? patch = null,
    DiffSet? diff = null,
    IReadOnlyList<IndirectChange>? changes = null,
    bool pull = true) =>
    new(HeadSha, head, @base, @base is null ? null : BaseSha, pull ? ForkSha : null, patch ?? PatchCoverage.Empty, diff ?? Diff(), changes ?? []);

  #endregion
}