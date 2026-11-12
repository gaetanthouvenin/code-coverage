// ------------------------------------------------------------------------------------------------
// <copyright file="SignInUserCommand.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using CodeCoverage.Application.Security;

namespace CodeCoverage.Application.Features.Access.Commands.SignInUser;

/// <summary>
///   Signs a user in with the tokens GitHub issued at the end of the OAuth flow: records the account, keeps its tokens
///   on the server side and writes its memberships.
/// </summary>
/// <param name="AccessToken">The user-to-server access token, in clear.</param>
/// <param name="AccessTokenLifetime">How long the access token lasts (<c>expires_in</c>), or <see langword="null" /> when it does not expire.</param>
/// <param name="RefreshToken">The refresh token, in clear, or <see langword="null" /> when GitHub gives none.</param>
/// <param name="RefreshTokenLifetime">How long the refresh token lasts (<c>refresh_token_expires_in</c>), or <see langword="null" />.</param>
/// <remarks>
///   <para>
///     Sent by the OAuth handler of the Web while nobody is signed in yet, hence <see cref="IAnonymousEntry" />: the
///     tokens themselves prove the account, which is read again from GitHub. It returns the identifier of the user, the
///     only value the cookie keeps.
///   </para>
///   <para>
///     It refuses with <c>Access.NoRepository</c> an account that reaches no repository of an active organization and
///     does not administer the instance. The user row and its tokens are kept all the same, so that a later refresh can
///     let the user in; the caller issues no session.
///   </para>
/// </remarks>
public sealed record SignInUserCommand(
  string AccessToken,
  TimeSpan? AccessTokenLifetime,
  string? RefreshToken,
  TimeSpan? RefreshTokenLifetime) : ICommand<long>, IAnonymousEntry
{
  #region Methods

  /// <summary>Returns the name only: the tokens never reach a log or a trace.</summary>
  /// <returns>The name of the command.</returns>
  public override string ToString() => nameof(SignInUserCommand);

  #endregion
}
