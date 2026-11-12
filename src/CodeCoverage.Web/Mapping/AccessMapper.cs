// ------------------------------------------------------------------------------------------------
// <copyright file="AccessMapper.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using CodeCoverage.Application.Features.Access.Queries.GetCurrentUser;
using CodeCoverage.Application.Features.Access.Queries.GetCurrentUserAccess;
using CodeCoverage.Application.Features.Organizations.Queries.ListMyOrganizations;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Users;
using CodeCoverage.Web.ViewModels.Access;

namespace CodeCoverage.Web.Mapping;

/// <summary>Converts the access between the Domain and the pages.</summary>
internal static class AccessMapper
{
  #region Methods

  /// <summary>Gets the signed-in user and converts it for the pages.</summary>
  /// <param name="sender">The request sender.</param>
  /// <param name="cancellationToken">A token to cancel the request.</param>
  /// <returns>The view model.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="sender" /> is <see langword="null" />.</exception>
  public static async Task<CurrentUserViewModel> GetCurrentUserViewModelAsync(this ISender sender, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(sender);

    var user = await sender.Send(new GetCurrentUserQuery(), cancellationToken).ConfigureAwait(false);

    return user.ToViewModel();
  }

  /// <summary>Gives the segment of a forge as the organizations carry it, from a segment or one of its aliases (ADR-014).</summary>
  /// <param name="segment">The segment of an address, e.g. <c>gh</c> or <c>github</c>.</param>
  /// <returns>The segment of the forge, e.g. <c>github</c>, or <see langword="null" /> when it names no forge.</returns>
  public static string? ToForgeSegment(string segment) =>
    ForgeSegments.TryParse(segment, out var forge) ? ForgeSegments.ToSegment(forge) : null;

  /// <summary>Reads what the navigation of the signed-in user shows and converts it for the pages.</summary>
  /// <param name="sender">The request sender.</param>
  /// <param name="cancellationToken">A token to cancel the request.</param>
  /// <returns>The navigation.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="sender" /> is <see langword="null" />.</exception>
  public static async Task<NavigationViewModel> GetNavigationViewModelAsync(this ISender sender, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(sender);

    var access = await sender.Send(new GetCurrentUserAccessQuery(), cancellationToken).ConfigureAwait(false);
    var organizations = await sender.Send(new ListMyOrganizationsQuery(), cancellationToken).ConfigureAwait(false);

    return new NavigationViewModel(
      access.User.Login,
      access.IsInstanceAdministrator,
      [
        .. organizations.Select(static m => new NavigationOrganizationViewModel(
                                  ForgeSegments.ToSegment(m.Organization.Forge),
                                  m.Organization.Forge.ToString(),
                                  m.Organization.Login,
                                  m.Role is OrganizationRole.Admin)),
      ]
    );
  }

  /// <summary>Converts a user, keeping only what a page shows: the protected tokens stay behind.</summary>
  /// <param name="user">The user.</param>
  /// <returns>The view model.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="user" /> is <see langword="null" />.</exception>
  public static CurrentUserViewModel ToViewModel(this AppUser user)
  {
    ArgumentNullException.ThrowIfNull(user);

    return new CurrentUserViewModel(user.Id, user.Login, user.AvatarUrl);
  }

  #endregion
}
