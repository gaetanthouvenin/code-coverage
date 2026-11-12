// ------------------------------------------------------------------------------------------------
// <copyright file="AccessMapper.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using CodeCoverage.Application.Features.Access.Queries.GetCurrentUser;
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
