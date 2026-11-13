// ------------------------------------------------------------------------------------------------
// <copyright file="GetAnnotatedSourceQueryHandler.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using CodeCoverage.Application.Exceptions;
using CodeCoverage.Application.Features.Commits;
using CodeCoverage.Application.Services;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Domain.Source;

namespace CodeCoverage.Application.Features.Commits.Queries.GetAnnotatedSource;

/// <summary>Handler for the <see cref="GetAnnotatedSourceQuery" /> query.</summary>
/// <param name="locator">Finds the repository of the address.</param>
/// <param name="commits">The commits of the repositories.</param>
/// <param name="reader">Reads the file in the report of the commit, or of one of its flags.</param>
/// <param name="sources">Reads the source from the forge, through its bounded cache.</param>
/// <param name="comparisons">The stored comparisons, for the patch of a pull request.</param>
/// <param name="diffs">The cached diffs, the only source of the patch lines: never the forge.</param>
/// <param name="reports">The stored reports, for the flags of the commit.</param>
/// <remarks>Read ports only: nothing is written, and the forge is the last thing asked.</remarks>
internal sealed class GetAnnotatedSourceQueryHandler(
  CommitLocator locator,
  ICommitRepository commits,
  CommitFileReader reader,
  ISourceContentCache sources,
  IComparisonRepository comparisons,
  IDiffCacheRepository diffs,
  ICommitReportRepository reports) : IQueryHandler<GetAnnotatedSourceQuery, AnnotatedSource>
{
  #region Implementation of IQueryHandler<GetAnnotatedSourceQuery,AnnotatedSource>

  /// <inheritdoc />
  /// <exception cref="NotFoundException">
  ///   The commit is unknown (<see cref="CommitErrors.NotFound" />), or its report has no such file
  ///   (<see cref="CommitErrors.FileNotFound" />).
  /// </exception>
  /// <exception cref="UnprocessableEntityException">The commit was compacted (<see cref="CommitErrors.Compacted" />).</exception>
  /// <exception cref="BadGatewayException">The forge refused the reading for another reason than its absence.</exception>
  public async ValueTask<AnnotatedSource> Handle(GetAnnotatedSourceQuery query, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(query);

    var repository = await locator.FindRepositoryAsync(query.Owner, query.Name, cancellationToken).ConfigureAwait(false);
    var commit = await commits.FindByShaAsync(repository.Id, query.Sha, cancellationToken).ConfigureAwait(false);

    NotFoundException.ThrowIfCommitNotFound(commit);

    var flag = string.IsNullOrEmpty(query.Flag) ? null : query.Flag;

    // The path must be one of the report: what reaches the forge is a stored path, never the visitor's text.
    var file = await reader.ReadAsync(commit, query.Path, flag, cancellationToken).ConfigureAwait(false);
    var text = await sources.GetSourceAsync(repository.ToForgeRef(query.Forge), commit.Sha, file.Path, cancellationToken).ConfigureAwait(false);

    var flags = await reports.ListFlagTotalsAsync(commit.Id, cancellationToken).ConfigureAwait(false);
    var source = AnnotatedSource.Merge(file, commit.Sha, flag, text) with
    {
      Flags = [.. flags.Select(static f => f.Flag).Where(static f => f.Length > 0).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)],
    };

    return query.Pull is { } pull && pull > 0 ? await WithPatchAsync(source, repository.Id, commit.Id, pull, file.Path, cancellationToken).ConfigureAwait(false) : source;
  }

  #endregion

  #region Methods

  // The patch the comparison of this commit in the pull request read: keyed on the fork point and this very commit, so
  // a later push of the pull request never marks the lines of another one. A miss marks nothing and reads no forge.
  private async Task<AnnotatedSource> WithPatchAsync(AnnotatedSource source, long repositoryId, long commitId, int pull, string path, CancellationToken cancellationToken)
  {
    var comparison = await comparisons.FindAsync(commitId, pull, cancellationToken).ConfigureAwait(false);

    if (comparison is not { ForkPointSha: { } forkPoint })
    {
      return source;
    }

    var diff = await diffs.FindAsync(repositoryId, forkPoint, source.Sha, comparison.DiffSource, cancellationToken).ConfigureAwait(false);
    var file = diff?.Files.FirstOrDefault(f => string.Equals(f.Path, path, StringComparison.Ordinal));

    return source.WithPatch(pull, file?.AddedLines);
  }

  #endregion
}
