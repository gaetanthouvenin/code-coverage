// ------------------------------------------------------------------------------------------------
// <copyright file="CurrentUserContext.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.Extensions.Options;

using CodeCoverage.Application.Configuration;
using CodeCoverage.Domain.Guards;
using CodeCoverage.Domain.Operations;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Domain.Users;

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
///   <para>
///     <see cref="IsAdmin" /> is computed at each read from <c>Access:AdminUserIds</c> and
///     <c>CodeCoverage__Bootstrap__AdminUserIds</c>, through <see cref="AdministratorList" />: a change of the
///     setting applies to the next read, without a new sign-in. A context built without the settings names no
///     administrator.
///   </para>
/// </remarks>
public sealed class CurrentUserContext : ICurrentUser
{
  #region Fields

  private readonly IOptionsMonitor<AccessOptions>? _access;

  private readonly IOptionsMonitor<BootstrapOptions>? _bootstrap;

  #endregion

  #region Ctors

  /// <summary>Initializes a new instance of the <see cref="CurrentUserContext" /> class, which names no administrator.</summary>
  public CurrentUserContext()
  {
  }

  /// <summary>Initializes a new instance of the <see cref="CurrentUserContext" /> class.</summary>
  /// <param name="access">The settings of the access, read at each check of the administration.</param>
  /// <param name="bootstrap">The bootstrap values of the access, read the same way.</param>
  /// <exception cref="ArgumentNullException"><paramref name="access" /> or <paramref name="bootstrap" /> is <see langword="null" />.</exception>
  public CurrentUserContext(IOptionsMonitor<AccessOptions> access, IOptionsMonitor<BootstrapOptions> bootstrap)
  {
    ArgumentNullException.ThrowIfNull(access);
    ArgumentNullException.ThrowIfNull(bootstrap);

    _access = access;
    _bootstrap = bootstrap;
  }

  #endregion

  #region Properties

  /// <inheritdoc />
  public long? UserId { get; private set; }

  /// <inheritdoc />
  public string? Login { get; private set; }

  /// <inheritdoc />
  public bool IsAuthenticated => UserId is not null;

  /// <inheritdoc />
  public bool IsAdmin =>
    Forge is { } forge
    && ExternalId is { } externalId
    && _access is not null
    && _bootstrap is not null
    && AdministratorList.Contains(forge, externalId, _access.CurrentValue.AdminUserIds, _bootstrap.CurrentValue.AdminUserIds);

  /// <summary>Gets the forge of the account of the signed-in user, or <see langword="null" />.</summary>
  public Forge? Forge { get; private set; }

  /// <summary>Gets the identifier the forge gives the account of the signed-in user, or <see langword="null" />.</summary>
  public string? ExternalId { get; private set; }

  /// <inheritdoc />
  public bool IsSystem { get; private set; }

  #endregion

  #region Methods

  /// <summary>Sets the signed-in user the scope runs for.</summary>
  /// <param name="userId">The identifier of the user.</param>
  /// <param name="login">The GitHub login of the user.</param>
  /// <param name="forge">The forge of the account of the user.</param>
  /// <param name="externalId">The identifier the forge gives the account, which the lists of administrators name.</param>
  /// <exception cref="ArgumentOutOfRangeException"><paramref name="userId" /> is not positive.</exception>
  /// <exception cref="ArgumentException"><paramref name="login" /> or <paramref name="externalId" /> is empty or white space.</exception>
  /// <exception cref="ArgumentNullException"><paramref name="login" /> or <paramref name="externalId" /> is <see langword="null" />.</exception>
  /// <exception cref="InvalidOperationException">The scope already runs for the system.</exception>
  public void SetUser(long userId, string login, Forge forge, string externalId)
  {
    ArgumentOutOfRangeException.ThrowIfNegativeOrZero(userId);
    ArgumentException.ThrowIfNullOrWhiteSpace(login);
    ArgumentException.ThrowIfNullOrWhiteSpace(externalId);

    InvalidOperationException.ThrowIf(IsSystem, "The scope already runs for the system: it cannot run for a user.");

    UserId = userId;
    Login = login;
    Forge = forge;
    ExternalId = externalId;
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
