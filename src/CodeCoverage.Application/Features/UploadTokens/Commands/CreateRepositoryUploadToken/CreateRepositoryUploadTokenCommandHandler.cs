// ------------------------------------------------------------------------------------------------
// <copyright file="CreateRepositoryUploadTokenCommandHandler.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using CodeCoverage.Application.Services;
using CodeCoverage.Domain.Tokens;

namespace CodeCoverage.Application.Features.UploadTokens.Commands.CreateRepositoryUploadToken;

/// <summary>Handler for the <see cref="CreateRepositoryUploadTokenCommand" /> command.</summary>
/// <param name="service">What the messages of the upload tokens share.</param>
internal sealed class CreateRepositoryUploadTokenCommandHandler(UploadTokenService service)
  : ICommandHandler<CreateRepositoryUploadTokenCommand, IssuedToken>
{
  #region Implementation of ICommandHandler<CreateRepositoryUploadTokenCommand,IssuedToken>

  /// <inheritdoc />
  public async ValueTask<IssuedToken> Handle(CreateRepositoryUploadTokenCommand command, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(command);

    var repositoryId = await service.GetRepositoryIdAsync(command.Owner, command.Name, cancellationToken).ConfigureAwait(false);

    return await service.CreateAsync(UploadTokenScope.Repository, repositoryId, command.Label, command.ImportedValue, command.ExpiresAt, cancellationToken)
                        .ConfigureAwait(false);
  }

  #endregion
}
