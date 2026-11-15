// ------------------------------------------------------------------------------------------------
// <copyright file="ReportGeneratorOracle.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Palmmedia.ReportGenerator.Core.Logging;
using Palmmedia.ReportGenerator.Core.Parser;
using Palmmedia.ReportGenerator.Core.Parser.Analysis;
using Palmmedia.ReportGenerator.Core.Parser.Filtering;

using CodeCoverage.Domain.Coverage;

namespace CodeCoverage.Infrastructure.Tests.Reports.Oracle;

// ReportGenerator reads the same report with its own, independent parsers. A class has its own view
// of a file, so a line of two classes is put together with the rule of CodeFile.Merge: the best status
// wins, and branches of the same identifier are one branch.
internal static class ReportGeneratorOracle
{
  #region Methods

  public static IReadOnlyDictionary<string, IReadOnlyDictionary<int, OracleLine>> Read(string reportPath) =>
    ParseFiles(reportPath)
      .GroupBy(file => file.Path, StringComparer.Ordinal)
      .ToDictionary(
        group => group.Key,
        group => (IReadOnlyDictionary<int, OracleLine>)ReadLines(group),
        StringComparer.Ordinal);

  // A code element of ReportGenerator, a method or a property, is covered when one of the lines from its
  // first to its last is covered in its file, all the classes of the file together.
  public static IReadOnlyDictionary<string, IReadOnlyList<OracleMethod>> ReadMethods(string reportPath) =>
    ParseFiles(reportPath)
      .GroupBy(file => file.Path, StringComparer.Ordinal)
      .ToDictionary(
        group => group.Key,
        group =>
        {
          var views = group.ToList();

          return (IReadOnlyList<OracleMethod>)
          [
            .. views.SelectMany(view => view.CodeElements)
                    .Select(element => new OracleMethod(element.FirstLine, IsCovered(views, element)))
                    .OrderBy(method => method.FirstLine),
          ];
        },
        StringComparer.Ordinal);

  public static (int CoveredBranches, int TotalBranches, int CoveredMethods, int TotalMethods) ReadDetail(string reportPath)
  {
    var files = ParseFiles(reportPath).ToList();

    return (
      files.Sum(file => file.CoveredBranches ?? 0),
      files.Sum(file => file.TotalBranches ?? 0),
      files.Sum(file => file.CoveredCodeElements),
      files.Sum(file => file.TotalCodeElements));
  }

  private static IEnumerable<CodeFile> ParseFiles(string reportPath)
  {
    LoggerFactory.VerbosityLevel = VerbosityLevel.Off;

    var all = new DefaultFilter(["+*"]);
    var parser = new CoverageReportParser(1, 1, [], all, all, all);

    return parser
      .ParseFiles([reportPath])
      .Assemblies.SelectMany(assembly => assembly.Classes)
      .SelectMany(@class => @class.Files);
  }

  private static bool IsCovered(List<CodeFile> views, CodeElement element) =>
    views.Any(
      view => Enumerable
        .Range(element.FirstLine, element.LastLine - element.FirstLine + 1)
        .Any(line => line < view.LineCoverage.Length && view.LineCoverage[line] > 0));

  private static Dictionary<int, OracleLine> ReadLines(IEnumerable<CodeFile> views)
  {
    var statuses = new Dictionary<int, LineVisitStatus>();
    var branches = new Dictionary<int, Dictionary<Branch, int>>();

    foreach (var view in views)
    {
      for (var line = 1; line < view.LineVisitStatus.Length; line++)
      {
        var status = view.LineVisitStatus[line];

        if (status is not LineVisitStatus.NotCoverable)
        {
          statuses[line] = (LineVisitStatus)Math.Max((int)status, (int)statuses.GetValueOrDefault(line));
        }
      }

      foreach (var (line, lineBranches) in view.BranchesByLine)
      {
        if (!branches.TryGetValue(line, out var known))
        {
          known = [];
          branches[line] = known;
        }

        foreach (var branch in lineBranches)
        {
          known[branch] = known.GetValueOrDefault(branch) + branch.BranchVisits;
        }
      }
    }

    return statuses.ToDictionary(
      pair => pair.Key,
      pair =>
      {
        var lineBranches = branches.GetValueOrDefault(pair.Key);

        return new OracleLine(
          ToState(pair.Value),
          lineBranches?.Values.Count(visits => visits > 0) ?? 0,
          lineBranches?.Count ?? 0);
      });
  }

  private static LineState ToState(LineVisitStatus status) =>
    status switch
    {
      LineVisitStatus.Covered => LineState.Hit,
      LineVisitStatus.PartiallyCovered => LineState.Partial,
      var _ => LineState.Miss,
    };

  #endregion
}
