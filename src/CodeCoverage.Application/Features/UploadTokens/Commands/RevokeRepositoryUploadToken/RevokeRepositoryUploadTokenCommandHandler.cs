// ------------------------------------------------------------------------------------------------
// <copyright file="RevokeRepositoryUploadTokenCommandHandler.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using CodeCoverage.Application.Services;
using CodeCoverage.Domain.Tokens;

namespace CodeCoverage.Application.Features.UploadTokens.Commands.RevokeRepositoryUploadToken;

/// <summary>Handler for the <see cref="RevokeRepositoryUploadTokenCommand" /> command.</summary>
/// <param name="service">What the messages of the upload tokens share.</param>
internal sealed class RevokeRepositoryUploadTokenCommandHandler(UploadTokenService service)
  : ICommandHandler<RevokeRepositoryUploadTokenCommand>
{
  #region Implementation of ICommandHandler<RevokeRepositoryUploadTokenCommand,Unit>

  /// <inheritdoc />
  public async ValueTask<Unit> Handle(RevokeRepositoryUploadTokenCommand command, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(command);

    var repositoryId = await service.GetRepositoryIdAsync(command.Owner, command.Name, cancellationToken).ConfigureAwait(false);

    await service.RevokeAsync(command.TokenId, UploadTokenScope.Repository, repositoryId, cancellationToken).ConfigureAwait(false);

    return Unit.Value;
  }

  #endregion
}
