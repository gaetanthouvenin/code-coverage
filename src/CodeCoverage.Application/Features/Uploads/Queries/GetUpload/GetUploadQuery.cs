// ------------------------------------------------------------------------------------------------
// <copyright file="GetUploadQuery.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using CodeCoverage.Application.Security;
using CodeCoverage.Domain.Commits;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Users;

namespace CodeCoverage.Application.Features.Uploads.Queries.GetUpload;

/// <summary>Reads an upload of a repository with a token of the API.</summary>
/// <param name="Forge">The forge of the repository.</param>
/// <param name="Owner">The owner segment of the address.</param>
/// <param name="Name">The name of the repository.</param>
/// <param name="UploadId">The public identifier of the upload.</param>
/// <remarks>
///   The counterpart of <c>GetUploadByCredential</c> for a reader of the repository: an upload of another repository
///   answers like one that does not exist.
/// </remarks>
public sealed record GetUploadQuery(Forge Forge, string Owner, string Name, Guid UploadId) : IQuery<Upload>, IRepositoryScoped
{
  #region Properties

  /// <inheritdoc />
  public RepositoryPermission RequiredPermission => RepositoryPermission.Read;

  #endregion
}
