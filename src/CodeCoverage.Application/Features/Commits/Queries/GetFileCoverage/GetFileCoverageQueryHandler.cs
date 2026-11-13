// ------------------------------------------------------------------------------------------------
// <copyright file="GetFileCoverageQueryHandler.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using CodeCoverage.Application.Exceptions;
using CodeCoverage.Application.Features.Commits;
using CodeCoverage.Application.Services;
using CodeCoverage.Domain.Coverage;

namespace CodeCoverage.Application.Features.Commits.Queries.GetFileCoverage;

/// <summary>Handler for the <see cref="GetFileCoverageQuery" /> query.</summary>
/// <param name="locator">Finds the commit of the address.</param>
/// <param name="reader">Reads the file in the report of the commit, or of one of its flags.</param>
internal sealed class GetFileCoverageQueryHandler(CommitLocator locator, CommitFileReader reader) : IQueryHandler<GetFileCoverageQuery, FileCoverageView>
{
  #region Implementation of IQueryHandler<GetFileCoverageQuery,FileCoverageView>

  /// <inheritdoc />
  /// <exception cref="UnprocessableEntityException">The commit was compacted (<see cref="CommitErrors.Compacted" />).</exception>
  /// <exception cref="NotFoundException">The report of the commit has no such file (<see cref="CommitErrors.FileNotFound" />).</exception>
  public async ValueTask<FileCoverageView> Handle(GetFileCoverageQuery query, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(query);

    var commit = await locator.FindCommitAsync(query.Owner, query.Name, query.Sha, cancellationToken).ConfigureAwait(false);
    var flag = string.IsNullOrEmpty(query.Flag) ? null : query.Flag;
    var file = await reader.ReadAsync(commit, query.Path, flag, cancellationToken).ConfigureAwait(false);

    return new FileCoverageView(file, flag);
  }

  #endregion
}
