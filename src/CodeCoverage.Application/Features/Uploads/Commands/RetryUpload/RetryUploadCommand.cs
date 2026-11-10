// ------------------------------------------------------------------------------------------------
// <copyright file="RetryUploadCommand.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using CodeCoverage.Application.Security;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Users;

namespace CodeCoverage.Application.Features.Uploads.Commands.RetryUpload;

/// <summary>Puts a failed upload back in the queue, so that it is parsed again from its raw files.</summary>
/// <param name="Forge">The forge of the repository.</param>
/// <param name="Owner">The owner of the repository, as its address writes it.</param>
/// <param name="Name">The name of the repository.</param>
/// <param name="UploadId">The public identifier of the upload.</param>
public sealed record RetryUploadCommand(Forge Forge, string Owner, string Name, Guid UploadId) : ICommand, IRepositoryScoped
{
  #region Properties

  /// <inheritdoc />
  public RepositoryPermission RequiredPermission => RepositoryPermission.Admin;

  #endregion
}
