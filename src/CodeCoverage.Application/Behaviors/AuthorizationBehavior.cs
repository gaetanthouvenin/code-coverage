// ------------------------------------------------------------------------------------------------
// <copyright file="AuthorizationBehavior.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using Microsoft.Extensions.DependencyInjection;

using CodeCoverage.Application.Exceptions;
using CodeCoverage.Application.Features.Organizations;
using CodeCoverage.Application.Features.Repositories;
using CodeCoverage.Application.Security;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Ports;

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
///       who administers the instance (<see cref="ForbiddenException" />,
///       <see cref="SecurityErrors.Forbidden" />);
///     </item>
///     <item>
///       an <see cref="IUserScoped" /> message requires a signed-in user
///       (<see cref="UnauthorizedException" />, <see cref="SecurityErrors.Unauthenticated" />);
///     </item>
///     <item>
///       an <see cref="IRepositoryScoped" /> or <see cref="IOrganizationScoped" /> message enters its
///       organization, then is refused (<see cref="NotFoundException" />,
///       <see cref="RepositoryErrors.NotFound" /> or <see cref="OrganizationErrors.NotFound" />) until the
///       access checks exist;
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
///   The refusal is logged by <see cref="LoggingBehavior{TMessage,TResponse}" />, which runs first.
/// </remarks>
/// <typeparam name="TMessage">The type of the message.</typeparam>
/// <typeparam name="TResponse">The type of the response.</typeparam>
/// <param name="currentUser">The principal the message is sent for.</param>
/// <param name="services">
///   The services of the scope. <see cref="IOrganizationScope" /> is resolved from them only for a message
///   that addresses an organization, so that the others never build what reads the database.
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

  #endregion

  #region Implementation of IPipelineBehavior<TMessage,TResponse>

  /// <inheritdoc />
  /// <exception cref="ForbiddenException">
  ///   The message is <see cref="ISystemOnly" /> and the sender is not the system, or it is
  ///   <see cref="IAdminOnly" /> and the signed-in user is not an administrator.
  /// </exception>
  /// <exception cref="UnauthorizedException">
  ///   The message is <see cref="IAdminOnly" /> or <see cref="IUserScoped" /> and nobody is signed in.
  /// </exception>
  /// <exception cref="NotFoundException">
  ///   The message is <see cref="IRepositoryScoped" /> or <see cref="IOrganizationScoped" /> and the sender
  ///   is not the system.
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

    if (IsSystemOnly)
    {
      throw new ForbiddenException(SecurityErrors.Forbidden, $"{MessageName} can only be sent by the system.");
    }

    if ((IsAdminOnly || IsUserScoped) && !currentUser.IsAuthenticated)
    {
      throw new UnauthorizedException(SecurityErrors.Unauthenticated, $"{MessageName} requires a signed-in user.");
    }

    if (IsAdminOnly && !currentUser.IsAdmin)
    {
      throw new ForbiddenException(SecurityErrors.Forbidden, $"{MessageName} requires an administrator.");
    }

    // No access check exists yet, so both scoped markers end in the same refusal whether the entry
    // succeeded or not: the response never depends on the state of the organization.
    switch (message)
    {
      case IRepositoryScoped repository:
        await TryEnterAsync(repository.Forge, OrganizationOf(repository.Owner), cancellationToken).ConfigureAwait(false);

        throw new NotFoundException(RepositoryErrors.NotFound, "The repository does not exist or cannot be read.");

      case IOrganizationScoped organization:
        await TryEnterAsync(organization.Forge, organization.Login, cancellationToken).ConfigureAwait(false);

        throw new NotFoundException(OrganizationErrors.NotFound, "The organization does not exist or cannot be read.");

      case IPublicOwnerAddressed addressed:
        await TryEnterAsync(addressed.Forge, OrganizationOf(addressed.Owner), cancellationToken).ConfigureAwait(false);

        break;
    }

    return await next(message, cancellationToken).ConfigureAwait(false);
  }

  #endregion
}
