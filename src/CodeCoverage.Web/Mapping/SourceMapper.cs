// ------------------------------------------------------------------------------------------------
// <copyright file="SourceMapper.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Globalization;

using Mediator;

using CodeCoverage.Application.Exceptions;
using CodeCoverage.Application.Features.Commits;
using CodeCoverage.Application.Features.Commits.Queries.GetAnnotatedSource;
using CodeCoverage.Application.Features.Repositories.Queries.GetEffectiveConfig;
using CodeCoverage.Domain.Config;
using CodeCoverage.Domain.Coverage;
using CodeCoverage.Domain.Git;
using CodeCoverage.Domain.Source;
using CodeCoverage.Web.ViewModels.Source;

namespace CodeCoverage.Web.Mapping;

/// <summary>Reads the annotated source of a file, for its page.</summary>
internal static class SourceMapper
{
  #region Methods

  /// <summary>Reads a source file at a commit with the coverage of its lines, formatted as the configuration says.</summary>
  /// <param name="sender">The request sender.</param>
  /// <param name="forge">The forge segment of the address.</param>
  /// <param name="owner">The owner segment of the address.</param>
  /// <param name="name">The name of the repository.</param>
  /// <param name="sha">The full SHA of the commit.</param>
  /// <param name="path">The path of the file.</param>
  /// <param name="flag">The flag, or <see langword="null" /> for every session.</param>
  /// <param name="pull">The pull request the page comes from, or <see langword="null" />.</param>
  /// <param name="cancellationToken">A token to cancel the reading.</param>
  /// <returns>The source, or <see langword="null" /> when the address is not one the console serves.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="sender" /> is <see langword="null" />.</exception>
  /// <remarks>
  ///   A compacted commit answers its own state instead of the refusal of the query: the page says so without a toast.
  ///   A full 40-character SHA only; a path is never empty.
  /// </remarks>
  public static async Task<AnnotatedSourceViewModel?> GetAnnotatedSourceViewModelAsync(
    this ISender sender,
    string forge,
    string owner,
    string name,
    string sha,
    string? path,
    string? flag,
    int? pull,
    CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(sender);

    if (!RepositoryMapper.TryParseForge(forge, out var parsed) || !GitSha.TryParse(sha, out var commitSha) || string.IsNullOrEmpty(path))
    {
      return null;
    }

    var query = new GetAnnotatedSourceQuery(parsed, owner, name, commitSha, path, string.IsNullOrEmpty(flag) ? null : flag, pull is > 0 ? pull : null);
    AnnotatedSource source;

    try
    {
      source = await sender.Send(query, cancellationToken).ConfigureAwait(false);
    }
    catch (UnprocessableEntityException exception) when (exception.ErrorCode == CommitErrors.Compacted)
    {
      return Compacted(path, commitSha);
    }

    var config = (await sender.Send(new GetEffectiveConfigQuery(parsed, owner, name), cancellationToken).ConfigureAwait(false)).Config;

    return ToViewModel(source, config);
  }

  private static AnnotatedSourceViewModel Compacted(string path, GitSha sha) =>
    new(path, DisplayFormatter.ShortSha(sha), [], null, true, false, DisplayFormatter.Dash, DisplayFormatter.Dash, DisplayFormatter.Dash, DisplayFormatter.Dash, DisplayFormatter.Dash, []);

  private static AnnotatedSourceViewModel ToViewModel(AnnotatedSource source, CoverageConfig config)
  {
    var totals = source.Totals;

    return new AnnotatedSourceViewModel(
      source.Path,
      DisplayFormatter.ShortSha(source.Sha),
      source.Flags ?? [],
      source.PatchPull,
      false,
      source.IsSourceAvailable,
      DisplayFormatter.Percent(totals.Coverage, config.Precision, config.Rounding),
      Count(totals.Lines),
      Count(totals.Hits),
      Count(totals.Partials),
      Count(totals.Misses),
      [.. source.Lines.Select(ToLine)]
    );
  }

  private static AnnotatedLineViewModel ToLine(AnnotatedLine line)
  {
    var state = line.State switch
    {
      LineState.Hit => SourceLineState.Hit,
      LineState.Miss => SourceLineState.Miss,
      LineState.Partial => SourceLineState.Partial,
      var _ => SourceLineState.None,
    };
    var hits = state switch
    {
      SourceLineState.None => null,
      SourceLineState.Partial when line.Total > 0 => string.Create(CultureInfo.InvariantCulture, $"{line.Covered}/{line.Total}"),
      var _ => line.Hits?.ToString(CultureInfo.InvariantCulture),
    };

    return new AnnotatedLineViewModel(line.Number, line.Text, state, hits, line.InPatch);
  }

  private static string Count(int value) => value.ToString("N0", CultureInfo.CurrentCulture);

  #endregion
}
