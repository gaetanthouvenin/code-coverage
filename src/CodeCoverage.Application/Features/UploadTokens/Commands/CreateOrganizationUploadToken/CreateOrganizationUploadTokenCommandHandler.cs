// ------------------------------------------------------------------------------------------------
// <copyright file="CreateOrganizationUploadTokenCommandHandler.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using CodeCoverage.Application.Services;
using CodeCoverage.Domain.Tokens;

namespace CodeCoverage.Application.Features.UploadTokens.Commands.CreateOrganizationUploadToken;

/// <summary>Handler for the <see cref="CreateOrganizationUploadTokenCommand" /> command.</summary>
/// <param name="service">What the messages of the upload tokens share.</param>
internal sealed class CreateOrganizationUploadTokenCommandHandler(UploadTokenService service)
  : ICommandHandler<CreateOrganizationUploadTokenCommand, IssuedToken>
{
  #region Implementation of ICommandHandler<CreateOrganizationUploadTokenCommand,IssuedToken>

  /// <inheritdoc />
  public async ValueTask<IssuedToken> Handle(CreateOrganizationUploadTokenCommand command, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(command);

    return await service.CreateAsync(UploadTokenScope.Organization, null, command.Label, command.ImportedValue, command.ExpiresAt, cancellationToken)
                        .ConfigureAwait(false);
  }

  #endregion
}
