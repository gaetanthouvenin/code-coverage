// ------------------------------------------------------------------------------------------------
// <copyright file="CheckRunAnnotationBuilder.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Globalization;

using CodeCoverage.Domain.Coverage;
using CodeCoverage.Domain.Diffs;

namespace CodeCoverage.Domain.Notifications;

/// <summary>Builds the annotations of the patch check run: the added lines the tests do not cover (M5.05).</summary>
/// <remarks>
///   <para>
///     Consecutive added lines of one file that are missed are grouped into one note, as are consecutive partial lines:
///     "Added line #L12 was not covered by tests", "Added lines #L12 - L15 were not covered by tests", "Added line #L20
///     was partially covered by tests". The wording follows the patch checks notifier of Codecov as recorded in the
///     research notes; its exact text was not compared with a real check run of Codecov.
///   </para>
///   <para>
///     A file without patch (binary, or a diff GitHub did not give) and a deleted file get no note. GitHub takes at
///     most <see cref="BatchSize" /> annotations per call: <see cref="Batch" /> cuts them.
///   </para>
/// </remarks>
public static class CheckRunAnnotationBuilder
{
  #region Fields

  /// <summary>The most annotations GitHub takes in one call that creates or updates a check run.</summary>
  public const int BatchSize = 50;

  #endregion

  #region Methods

  /// <summary>Builds the annotations of a patch.</summary>
  /// <param name="head">The view of the head report, merged as the project status reads it.</param>
  /// <param name="patch">The diff of the patch.</param>
  /// <returns>The annotations, by path then line.</returns>
  /// <exception cref="ArgumentNullException">An argument is <see langword="null" />.</exception>
  public static IReadOnlyList<CheckRunAnnotation> Build(ReportView head, DiffSet patch)
  {
    ArgumentNullException.ThrowIfNull(head);
    ArgumentNullException.ThrowIfNull(patch);

    var files = head.Report.Files.ToDictionary(static f => f.Path, StringComparer.Ordinal);
    var annotations = new List<CheckRunAnnotation>();

    foreach (var file in patch.Files.Where(static f => f.HasPatch && f.Status is not FileDiffStatus.Removed).OrderBy(static f => f.Path, StringComparer.Ordinal))
    {
      if (!files.TryGetValue(file.Path, out var coverage))
      {
        continue;
      }

      var added = new HashSet<int>(file.AddedLines);
      var lines = coverage.Lines.Where(l => added.Contains(l.Line)).OrderBy(static l => l.Line);
      (int Start, int End, bool Partial)? group = null;

      foreach (var line in lines)
      {
        // The classification of the patch status: the annotations never contradict codecov/patch.
        bool? partial = CoverageMath.Classify(line) switch
        {
          LineState.Miss => false,
          LineState.Partial => true,
          var _ => null,
        };

        if (partial is not { } isPartial)
        {
          Flush(annotations, file.Path, ref group);

          continue;
        }

        if (group is { } current && current.Partial == isPartial && current.End + 1 == line.Line)
        {
          group = current with { End = line.Line };
        }
        else
        {
          Flush(annotations, file.Path, ref group);
          group = (line.Line, line.Line, isPartial);
        }
      }

      Flush(annotations, file.Path, ref group);
    }

    return annotations;
  }

  /// <summary>Cuts annotations into the batches of one call each.</summary>
  /// <param name="annotations">The annotations.</param>
  /// <returns>The batches of <see cref="BatchSize" /> annotations at most, in order; none without annotation.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="annotations" /> is <see langword="null" />.</exception>
  public static IReadOnlyList<IReadOnlyList<CheckRunAnnotation>> Batch(IReadOnlyList<CheckRunAnnotation> annotations)
  {
    ArgumentNullException.ThrowIfNull(annotations);

    return [.. annotations.Chunk(BatchSize).Select(static chunk => (IReadOnlyList<CheckRunAnnotation>)chunk)];
  }

  /// <summary>Writes the note of a group, if there is one, and forgets the group.</summary>
  /// <param name="annotations">The notes.</param>
  /// <param name="path">The path of the file.</param>
  /// <param name="group">The group of lines.</param>
  private static void Flush(List<CheckRunAnnotation> annotations, string path, ref (int Start, int End, bool Partial)? group)
  {
    if (group is not { } current)
    {
      return;
    }

    var how = current.Partial ? "partially covered" : "not covered";
    var message = current.Start == current.End
                    ? string.Create(CultureInfo.InvariantCulture, $"Added line #L{current.Start} was {how} by tests")
                    : string.Create(CultureInfo.InvariantCulture, $"Added lines #L{current.Start} - L{current.End} were {how} by tests");

    annotations.Add(new CheckRunAnnotation(path, current.Start, current.End, message));
    group = null;
  }

  #endregion
}
