// ------------------------------------------------------------------------------------------------
// <copyright file="GetCommitFilesQuery.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using CodeCoverage.Application.Security;
using CodeCoverage.Domain.Coverage;
using CodeCoverage.Domain.Git;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Users;

namespace CodeCoverage.Application.Features.Commits.Queries.GetCommitFiles;

/// <summary>Lists the files of the report of a commit, with their totals, sorted by path.</summary>
/// <param name="Forge">The forge of the repository.</param>
/// <param name="Owner">The owner segment of the address.</param>
/// <param name="Name">The name of the repository.</param>
/// <param name="Sha">The SHA of the commit.</param>
/// <param name="Flag">The flag to read the files for, or <see langword="null" /> for the merged report.</param>
/// <param name="Path">A prefix the paths must start with, or <see langword="null" /> for every file.</param>
/// <remarks>
///   Without flag, the index of the files is read without decoding any line. With a flag, the reports of the sessions are
///   read: a compacted commit then answers 422 <c>Commit.Compacted</c>.
/// </remarks>
public sealed record GetCommitFilesQuery(Forge Forge, string Owner, string Name, GitSha Sha, string? Flag, string? Path)
  : IQuery<IReadOnlyList<FileTotals>>, IRepositoryScoped
{
  #region Properties

  /// <inheritdoc />
  public RepositoryPermission RequiredPermission => RepositoryPermission.Read;

  #endregion
}
