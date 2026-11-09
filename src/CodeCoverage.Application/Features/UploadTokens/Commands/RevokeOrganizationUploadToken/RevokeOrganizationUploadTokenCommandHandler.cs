// ------------------------------------------------------------------------------------------------
// <copyright file="RevokeOrganizationUploadTokenCommandHandler.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using CodeCoverage.Application.Services;
using CodeCoverage.Domain.Tokens;

namespace CodeCoverage.Application.Features.UploadTokens.Commands.RevokeOrganizationUploadToken;

/// <summary>Handler for the <see cref="RevokeOrganizationUploadTokenCommand" /> command.</summary>
/// <param name="service">What the messages of the upload tokens share.</param>
internal sealed class RevokeOrganizationUploadTokenCommandHandler(UploadTokenService service)
  : ICommandHandler<RevokeOrganizationUploadTokenCommand>
{
  #region Implementation of ICommandHandler<RevokeOrganizationUploadTokenCommand,Unit>

  /// <inheritdoc />
  public async ValueTask<Unit> Handle(RevokeOrganizationUploadTokenCommand command, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(command);

    await service.RevokeAsync(command.TokenId, UploadTokenScope.Organization, null, cancellationToken).ConfigureAwait(false);

    return Unit.Value;
  }

  #endregion
}
