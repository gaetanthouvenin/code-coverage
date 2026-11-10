// ------------------------------------------------------------------------------------------------
// <copyright file="CurrentUserContext.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Guards;
using CodeCoverage.Domain.Operations;
using CodeCoverage.Domain.Ports;

namespace CodeCoverage.Infrastructure.Identity;

/// <summary>
///   The principal of the current DI scope (an HTTP request, a circuit send or a job), set once by
///   whoever opens the scope. Anonymous until then.
/// </summary>
/// <remarks>
///   <para>
///     The hosts set a signed-in user with <see cref="SetUser" />. Only the Infrastructure (the
///     job worker and the runner of the administration command line) can set the system principal,
///     through the <see langword="internal" /> <see cref="SetSystem" /> and
///     <see cref="SetCommandLine" />: the compiler keeps the Api and the Web from ever producing it.
///   </para>
///   <para>
///     A scope runs for one principal: once a user or the system is set, the context refuses to
///     switch to the other, so that a user scope can never be elevated to the system.
///   </para>
/// </remarks>
public sealed class CurrentUserContext : ICurrentUser
{
  #region Properties

  /// <inheritdoc />
  public long? UserId { get; private set; }

  /// <inheritdoc />
  public string? Login { get; private set; }

  /// <inheritdoc />
  public bool IsAuthenticated => UserId is not null;

  /// <inheritdoc />
  public bool IsAdmin { get; private set; }

  /// <inheritdoc />
  public bool IsSystem { get; private set; }

  #endregion

  #region Methods

  /// <summary>Sets the signed-in user the scope runs for.</summary>
  /// <param name="userId">The identifier of the user.</param>
  /// <param name="login">The GitHub login of the user.</param>
  /// <param name="isAdmin">Whether the user administers the instance.</param>
  /// <exception cref="ArgumentOutOfRangeException"><paramref name="userId" /> is not positive.</exception>
  /// <exception cref="ArgumentException"><paramref name="login" /> is empty or white space.</exception>
  /// <exception cref="ArgumentNullException"><paramref name="login" /> is <see langword="null" />.</exception>
  /// <exception cref="InvalidOperationException">The scope already runs for the system.</exception>
  public void SetUser(long userId, string login, bool isAdmin)
  {
    ArgumentOutOfRangeException.ThrowIfNegativeOrZero(userId);
    ArgumentException.ThrowIfNullOrWhiteSpace(login);

    InvalidOperationException.ThrowIf(IsSystem, "The scope already runs for the system: it cannot run for a user.");

    UserId = userId;
    Login = login;
    IsAdmin = isAdmin;
  }

  /// <summary>Sets the system principal, which passes every authorization check.</summary>
  /// <exception cref="InvalidOperationException">The scope already runs for a user.</exception>
  /// <remarks>Called by the job worker only, in the scope it creates for each job.</remarks>
  internal void SetSystem()
  {
    InvalidOperationException.ThrowIf(
      IsAuthenticated,
      "The scope already runs for a user: it cannot run for the system."
    );

    IsSystem = true;
  }

  /// <summary>Sets the system principal of the administration command line, which names itself in the audit trail.</summary>
  /// <exception cref="InvalidOperationException">The scope already runs for a user.</exception>
  /// <remarks>
  ///   Called by <c>SystemCommandRunner</c> only. <see cref="Login" /> then holds
  ///   <see cref="AuditEntry.CommandLineActor" />, which no forge login can be.
  /// </remarks>
  internal void SetCommandLine()
  {
    SetSystem();

    Login = AuditEntry.CommandLineActor;
  }

  #endregion
}
