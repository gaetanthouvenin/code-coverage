// ------------------------------------------------------------------------------------------------
// <copyright file="ListCommitUploadsQuery.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using CodeCoverage.Application.Security;
using CodeCoverage.Domain.Commits;
using CodeCoverage.Domain.Git;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Users;

namespace CodeCoverage.Application.Features.Uploads.Queries.ListCommitUploads;

/// <summary>Lists the uploads of a commit.</summary>
/// <param name="Forge">The forge of the repository.</param>
/// <param name="Owner">The owner segment of the address.</param>
/// <param name="Name">The name of the repository.</param>
/// <param name="Sha">The SHA of the commit.</param>
public sealed record ListCommitUploadsQuery(Forge Forge, string Owner, string Name, GitSha Sha) : IQuery<IReadOnlyList<Upload>>, IRepositoryScoped
{
  #region Properties

  /// <inheritdoc />
  public RepositoryPermission RequiredPermission => RepositoryPermission.Read;

  #endregion
}
