// ------------------------------------------------------------------------------------------------
// <copyright file="CreateApiTokenCommandHandler.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Globalization;

using Mediator;

using CodeCoverage.Application.Exceptions;
using CodeCoverage.Application.Features.Organizations;
using CodeCoverage.Application.Security;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Domain.Tokens;

namespace CodeCoverage.Application.Features.Access.Commands.CreateApiToken;

/// <summary>Handler for the <see cref="CreateApiTokenCommand" /> command.</summary>
/// <param name="currentUser">The principal of the scope.</param>
/// <param name="tokens">The repository of the personal tokens.</param>
/// <param name="directory">The directory of the organizations.</param>
/// <param name="roles">Tells the role of a user in an organization.</param>
/// <param name="generator">Generates the value of the token.</param>
/// <param name="hasher">Hashes the value of the token.</param>
/// <param name="auditLog">The audit trail.</param>
/// <param name="clock">The clock.</param>
/// <remarks>
///   <para>
///     Only the SHA-256 of the value is saved; the value is returned this one time. The hint keeps the prefix and four
///     characters, too few to guess the rest.
///   </para>
///   <para>
///     An organization that does not exist, is not active, or in which the user holds no role is refused as unknown
///     (<see cref="OrganizationErrors.NotFound" />), so that the answer reveals nothing of an organization the user does
///     not belong to. The tables read are global: no organization is entered.
///   </para>
/// </remarks>
internal sealed class CreateApiTokenCommandHandler(
  ICurrentUser currentUser,
  IApiTokenRepository tokens,
  IOrganizationDirectory directory,
  IOrganizationRoleResolver roles,
  ITokenGenerator generator,
  ITokenHasher hasher,
  IAuditLog auditLog,
  IClock clock) : ICommandHandler<CreateApiTokenCommand, IssuedApiToken>
{
  #region Fields

  /// <summary>The audit action of a created token.</summary>
  internal const string AuditAction = "ApiTokens.Create";

  /// <summary>How many characters of the value the hint keeps: the prefix and four characters.</summary>
  private const int HintLength = 8;

  #endregion

  #region Methods

  /// <summary>Finds the organization of a token of an organization, where the user holds a role.</summary>
  /// <param name="userId">The identifier of the user.</param>
  /// <param name="forge">The forge of the organization.</param>
  /// <param name="login">The login of the organization.</param>
  /// <param name="cancellationToken">A token to cancel the reading.</param>
  /// <returns>The identifier of the organization.</returns>
  /// <exception cref="NotFoundException">The organization is unknown, not active, or the user holds no role in it.</exception>
  private async Task<long> FindOrganizationAsync(long userId, Forge forge, string login, CancellationToken cancellationToken)
  {
    var found = await directory.FindByLoginAsync(forge, login, cancellationToken).ConfigureAwait(false);
    var organization = found is { State: OrganizationState.Active }
                       && (currentUser.TokenOrganizationId is not { } tokenOrganization || tokenOrganization == found.Id)
                         ? found
                         : null;
    var role = organization is null ? null : await roles.ResolveAsync(userId, organization.Id, cancellationToken).ConfigureAwait(false);

    NotFoundException.ThrowIfOrganizationNotFound(role);

    return organization!.Id;
  }

  #endregion

  #region Implementation of ICommandHandler<CreateApiTokenCommand,IssuedApiToken>

  /// <inheritdoc />
  /// <exception cref="ForbiddenException">
  ///   A token of the instance is asked by a user who does not administer the instance, or from a token of an
  ///   organization; or a token of an organization is asked from a token of the instance.
  /// </exception>
  /// <exception cref="NotFoundException">The organization is unknown, not active, or the user holds no role in it.</exception>
  public async ValueTask<IssuedApiToken> Handle(CreateApiTokenCommand command, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(command);

    var userId = currentUser.UserId;

    UnauthorizedException.ThrowIfNoUser(userId);

    long? organizationId;

    if (command.Forge is { } forge && command.Organization is { } login)
    {
      ForbiddenException.ThrowIfOrganizationApiTokenRefused(currentUser.IsInstanceToken);

      organizationId = await FindOrganizationAsync(userId.Value, forge, login, cancellationToken).ConfigureAwait(false);
    }
    else
    {
      ForbiddenException.ThrowIfInstanceApiTokenRefused(!currentUser.IsAdmin || currentUser.TokenOrganizationId is not null);

      organizationId = null;
    }

    var value = generator.NewApiToken();
    var token = ApiToken.Create(userId.Value, organizationId, command.Name.Trim(), value[..HintLength], hasher.Hash(value), clock.UtcNow, command.ExpiresAt);

    await tokens.AddAsync(token, cancellationToken).ConfigureAwait(false);

    var details = new Dictionary<string, string>(StringComparer.Ordinal)
    {
      ["Name"] = token.Name,
      ["Organization"] = organizationId?.ToString(CultureInfo.InvariantCulture) ?? "instance",
    };

    await auditLog.RecordAsync(AuditAction, token.Id.ToString(CultureInfo.InvariantCulture), details, cancellationToken).ConfigureAwait(false);

    return new IssuedApiToken(token, value);
  }

  #endregion
}
