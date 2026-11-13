// ------------------------------------------------------------------------------------------------
// <copyright file="AuthorizationBehavior.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using Microsoft.Extensions.DependencyInjection;

using CodeCoverage.Application.Exceptions;
using CodeCoverage.Application.Features.Access;
using CodeCoverage.Application.Features.Organizations;
using CodeCoverage.Application.Features.Repositories;
using CodeCoverage.Application.Security;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Domain.Repositories;
using CodeCoverage.Domain.Users;

namespace CodeCoverage.Application.Behaviors;

/// <summary>
///   Pipeline behavior that enters the organization a message addresses and refuses the message, before
///   its validation and its handler, when the current principal may not send it.
/// </summary>
/// <remarks>
///   <list type="bullet">
///     <item>the system principal (jobs, webhook processing) passes every check and keeps its scope;</item>
///     <item>
///       an <see cref="ISystemOnly" /> message sent by anyone else is refused
///       (<see cref="ForbiddenException" />, <see cref="SecurityErrors.Forbidden" />);
///     </item>
///     <item>
///       an <see cref="IAdminOnly" /> message requires a signed-in user
///       (<see cref="UnauthorizedException" />, <see cref="SecurityErrors.Unauthenticated" />)
///       who administers the instance, and not through a token of an organization
///       (<see cref="ForbiddenException" />, <see cref="SecurityErrors.Forbidden" />);
///     </item>
///     <item>
///       an <see cref="IUserScoped" /> message requires a signed-in user
///       (<see cref="UnauthorizedException" />, <see cref="SecurityErrors.Unauthenticated" />);
///     </item>
///     <item>
///       an <see cref="IRepositoryScoped" /> message enters its organization, then requires a signed-in user who
///       may read the repository, active and tracked (<see cref="IRepositoryAccessService" />): otherwise
///       <see cref="NotFoundException" /> (<see cref="RepositoryErrors.NotFound" />), the same whatever the cause, so
///       that a private repository cannot be told from a missing one. A user who may read it but lacks the
///       permission the message requires is refused with <see cref="ForbiddenException" />
///       (<see cref="SecurityErrors.Forbidden" />);
///     </item>
///     <item>
///       an <see cref="IOrganizationScoped" /> message enters its organization, then requires a signed-in user who
///       holds a role there (<see cref="IOrganizationRoleResolver" />): otherwise <see cref="NotFoundException" />
///       (<see cref="OrganizationErrors.NotFound" />). A participant sending a message that requires an administrator
///       is refused with <see cref="ForbiddenException" /> (<see cref="SecurityErrors.Forbidden" />);
///     </item>
///     <item>
///       an <see cref="IPublicOwnerAddressed" /> message enters its organization when it is active, and
///       otherwise passes with the scope unset;
///     </item>
///     <item>
///       an <see cref="IAnonymousEntry" /> message passes, as does a message without marker, which
///       <c>MessageMarkerTests</c> keeps out of the Application.
///     </item>
///   </list>
///   A request authenticated by a personal token of the API enters the organization of its token only, and a token of
///   the instance enters none: <see cref="IOrganizationScope" /> refuses any other entry, which ends in the
///   <see cref="NotFoundException" /> of the marker (M3.05).
///   The administration of the instance grants nothing on the data of an organization (ADR-013). The refusal is
///   logged by <see cref="LoggingBehavior{TMessage,TResponse}" />, which runs first.
/// </remarks>
/// <typeparam name="TMessage">The type of the message.</typeparam>
/// <typeparam name="TResponse">The type of the response.</typeparam>
/// <param name="currentUser">The principal the message is sent for.</param>
/// <param name="services">
///   The services of the scope. <see cref="IOrganizationScope" /> and the services of the access checks are
///   resolved from them only for a message that addresses an organization, so that the others never build what
///   reads the database.
/// </param>
internal sealed class AuthorizationBehavior<TMessage, TResponse>(
  ICurrentUser currentUser,
  IServiceProvider services) : IPipelineBehavior<TMessage, TResponse>
  where TMessage : IMessage
{
  #region Fields

  private static readonly bool IsAdminOnly = typeof(IAdminOnly).IsAssignableFrom(typeof(TMessage));

  private static readonly bool IsSystemOnly = typeof(ISystemOnly).IsAssignableFrom(typeof(TMessage));

  private static readonly bool IsUserScoped = typeof(IUserScoped).IsAssignableFrom(typeof(TMessage));

  private static readonly string MessageName = typeof(TMessage).Name;

  #endregion

  #region Methods

  // An owner reads "organization:namespace" on the forges with a level under the organization (ADR-014).
  // The value is raw, as no validator has run yet: a null or malformed one is left to the entry to refuse.
  private static string? OrganizationOf(string? owner)
  {
    return owner?.Split(':', 2)[0];
  }

  private Task<bool> TryEnterAsync(Forge forge, string? login, CancellationToken cancellationToken)
  {
    return services.GetRequiredService<IOrganizationScope>().TryEnterAsync(forge, login, cancellationToken);
  }

  /// <summary>Enters the organization of a repository and checks the permission of the user on it.</summary>
  /// <param name="message">The message.</param>
  /// <param name="cancellationToken">A token to cancel the checks.</param>
  /// <returns>A task that completes when the message may go on.</returns>
  /// <exception cref="NotFoundException">The user may not read the repository, whatever the cause.</exception>
  /// <exception cref="ForbiddenException">The user may read the repository but lacks the required permission.</exception>
  private async Task AuthorizeRepositoryAsync(IRepositoryScoped message, CancellationToken cancellationToken)
  {
    var entered = await TryEnterAsync(message.Forge, OrganizationOf(message.Owner), cancellationToken).ConfigureAwait(false);
    var userId = currentUser.UserId;
    var tracked = entered && userId is not null && RepositorySlug.TryFromAddress(message.Owner, message.Name, out var slug)
                    ? await services.GetRequiredService<IRepositoryRepository>().FindBySlugAsync(slug, cancellationToken).ConfigureAwait(false)
                    : null;

    // An inactive or departed repository answers as a missing one: one refusal, whatever the cause.
    var repository = tracked is { IsActive: true } ? tracked : null;

    NotFoundException.ThrowIfRepositoryNotFound(repository);

    var access = services.GetRequiredService<IRepositoryAccessService>();
    var readable = await access.CanAsync(userId!.Value, repository.ExternalId, RepositoryPermission.Read, cancellationToken).ConfigureAwait(false);

    NotFoundException.ThrowIfRepositoryNotReadable(readable);

    var granted = await RepositoryAuthorization.HasPermissionAsync(
                          message.RequiredPermission,
                          userId.Value,
                          repository.ExternalId,
                          access,
                          services.GetRequiredService<IOrganizationRoleResolver>(),
                          services.GetRequiredService<ICurrentOrganization>(),
                          cancellationToken)
                        .ConfigureAwait(false);

    ForbiddenException.ThrowIfRepositoryPermissionMissing(!granted, MessageName, message.RequiredPermission);
  }

  /// <summary>Enters an organization and checks the role of the user in it.</summary>
  /// <param name="message">The message.</param>
  /// <param name="cancellationToken">A token to cancel the checks.</param>
  /// <returns>A task that completes when the message may go on.</returns>
  /// <exception cref="NotFoundException">The user holds no role in the organization, whatever the cause.</exception>
  /// <exception cref="ForbiddenException">The user takes part in the organization but the message requires an administrator.</exception>
  private async Task AuthorizeOrganizationAsync(IOrganizationScoped message, CancellationToken cancellationToken)
  {
    var entered = await TryEnterAsync(message.Forge, message.Login, cancellationToken).ConfigureAwait(false);
    var role = entered && currentUser.UserId is { } userId
                 ? await services.GetRequiredService<IOrganizationRoleResolver>()
                                 .ResolveAsync(userId, services.GetRequiredService<ICurrentOrganization>().Id, cancellationToken)
                                 .ConfigureAwait(false)
                 : null;

    NotFoundException.ThrowIfOrganizationNotFound(role);
    ForbiddenException.ThrowIfOrganizationRoleMissing(message.RequiredRole is OrganizationRole.Admin && role is not OrganizationRole.Admin, MessageName);
  }

  #endregion

  #region Implementation of IPipelineBehavior<TMessage,TResponse>

  /// <inheritdoc />
  /// <exception cref="ForbiddenException">
  ///   The message is <see cref="ISystemOnly" /> and the sender is not the system, it is
  ///   <see cref="IAdminOnly" /> and the signed-in user is not an administrator, or the user lacks the permission
  ///   or the role a scoped message requires.
  /// </exception>
  /// <exception cref="UnauthorizedException">
  ///   The message is <see cref="IAdminOnly" /> or <see cref="IUserScoped" /> and nobody is signed in.
  /// </exception>
  /// <exception cref="NotFoundException">
  ///   The message is <see cref="IRepositoryScoped" /> or <see cref="IOrganizationScoped" /> and the user may not
  ///   read what it addresses.
  /// </exception>
  public async ValueTask<TResponse> Handle(
    TMessage message,
    MessageHandlerDelegate<TMessage, TResponse> next,
    CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(message);
    ArgumentNullException.ThrowIfNull(next);

    if (currentUser.IsSystem)
    {
      return await next(message, cancellationToken).ConfigureAwait(false);
    }

    ForbiddenException.ThrowIfNotSystem(IsSystemOnly, MessageName);
    UnauthorizedException.ThrowIfAnonymous(!(IsAdminOnly || IsUserScoped) || currentUser.IsAuthenticated, MessageName);

    // A token of an organization stays within it: the administration of the instance needs a token of the instance.
    ForbiddenException.ThrowIfNotInstanceAdministrator(IsAdminOnly && (!currentUser.IsAdmin || currentUser.TokenOrganizationId is not null), MessageName);

    switch (message)
    {
      case IRepositoryScoped repository:
        await AuthorizeRepositoryAsync(repository, cancellationToken).ConfigureAwait(false);

        break;

      case IOrganizationScoped organization:
        await AuthorizeOrganizationAsync(organization, cancellationToken).ConfigureAwait(false);

        break;

      case IPublicOwnerAddressed addressed:
        await TryEnterAsync(addressed.Forge, OrganizationOf(addressed.Owner), cancellationToken).ConfigureAwait(false);

        break;
    }

    return await next(message, cancellationToken).ConfigureAwait(false);
  }

  #endregion
}
