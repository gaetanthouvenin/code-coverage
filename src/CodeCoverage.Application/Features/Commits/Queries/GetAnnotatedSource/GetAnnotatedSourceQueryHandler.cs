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
/// <remarks>Read ports only: nothing is written, and the forge is the last thing asked.</remarks>
internal sealed class GetAnnotatedSourceQueryHandler(
  CommitLocator locator,
  ICommitRepository commits,
  CommitFileReader reader,
  ISourceContentCache sources) : IQueryHandler<GetAnnotatedSourceQuery, AnnotatedSource>
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

    return AnnotatedSource.Merge(file, commit.Sha, flag, text);
  }

  #endregion
}
