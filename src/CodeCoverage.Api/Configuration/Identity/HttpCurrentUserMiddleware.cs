// ------------------------------------------------------------------------------------------------
// <copyright file="HttpCurrentUserMiddleware.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Globalization;
using System.Security.Claims;

using CodeCoverage.Infrastructure.Identity;

namespace CodeCoverage.Api.Configuration.Identity;

/// <summary>Sets the principal of the request scope from the claims of the authenticated user.</summary>
/// <remarks>
///   <para>
///     A user is set only when the request is authenticated and carries a positive numeric
///     <see cref="ClaimTypes.NameIdentifier" /> and a non-blank <see cref="ClaimTypes.Name" />; the
///     <see cref="AdminRole" /> role makes them an administrator. Otherwise the scope stays
///     anonymous.
///   </para>
///   <para>
///     No authentication scheme is registered yet: every request is anonymous until the
///     <c>ApiToken</c> scheme exists. The system principal can never be produced here.
///   </para>
/// </remarks>
/// <param name="next">The rest of the pipeline.</param>
internal sealed class HttpCurrentUserMiddleware(RequestDelegate next)
{
  #region Fields

  /// <summary>The role of the administrators of the instance.</summary>
  public const string AdminRole = "Admin";

  #endregion

  #region Methods

  /// <summary>Sets the user of the scope, then runs the rest of the pipeline.</summary>
  /// <param name="context">The request.</param>
  /// <param name="currentUser">The principal of the request scope.</param>
  /// <returns>A task that completes with the request.</returns>
  /// <exception cref="ArgumentNullException">
  ///   <paramref name="context" /> or <paramref name="currentUser" /> is <see langword="null" />.
  /// </exception>
  public Task InvokeAsync(HttpContext context, CurrentUserContext currentUser)
  {
    ArgumentNullException.ThrowIfNull(context);
    ArgumentNullException.ThrowIfNull(currentUser);

    var user = context.User;

    if (user.Identity?.IsAuthenticated == true
        && long.TryParse(
          user.FindFirstValue(ClaimTypes.NameIdentifier),
          NumberStyles.None,
          CultureInfo.InvariantCulture,
          out var userId)
        && userId > 0
        && user.FindFirstValue(ClaimTypes.Name) is { } login
        && !string.IsNullOrWhiteSpace(login))
    {
      currentUser.SetUser(userId, login, user.IsInRole(AdminRole));
    }

    return next(context);
  }

  #endregion
}
