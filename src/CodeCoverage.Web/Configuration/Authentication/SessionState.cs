// ------------------------------------------------------------------------------------------------
// <copyright file="SessionState.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Infrastructure.Identity;

namespace CodeCoverage.Web.Configuration.Authentication;

/// <summary>Tells the pages whether someone is signed in, and whether a session was presented then rejected.</summary>
/// <param name="currentUser">The principal of the scope: of the request while prerendering, of the circuit after.</param>
/// <remarks>
///   <para>
///     A visitor who never signed in and a user whose session ended are both anonymous: only
///     <see cref="WasRejected" /> tells them apart, so that « your session has expired » is shown after a real session
///     only.
///   </para>
///   <para>
///     <see cref="SessionCookieEvents" /> rejects the cookie in the scope of the request, which the prerendering of the
///     page shares; the cookie is also removed, so the next request is a plain anonymous one.
///   </para>
/// </remarks>
public sealed class SessionState(CurrentUserContext currentUser)
{
  #region Properties

  /// <summary>Gets a value indicating whether a user is signed in.</summary>
  public bool IsSignedIn => currentUser.IsAuthenticated;

  /// <summary>Gets a value indicating whether the request presented a session cookie that was rejected.</summary>
  public bool WasRejected { get; private set; }

  #endregion

  #region Methods

  /// <summary>Records that the session cookie of the request was rejected.</summary>
  internal void Reject() => WasRejected = true;

  #endregion
}
