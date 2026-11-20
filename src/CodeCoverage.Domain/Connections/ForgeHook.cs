// ------------------------------------------------------------------------------------------------
// <copyright file="ForgeHook.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Guards;

namespace CodeCoverage.Domain.Connections;

/// <summary>
///   The state of a hook that a forge connection created at its forge, for the whole account, a project or a
///   repository; one row serves the three forges with connections.
/// </summary>
/// <remarks>
///   <para>
///     A hook belongs to the organization of its connection, and its scope is unique within the connection: the hook
///     of a scope is checked again and its state replaced, never added twice.
///   </para>
///   <para>
///     The identifier of the scope is kept in lower case, so that it compares the same way under the collations of
///     both database engines. The identifier of the hook is the one the forge returned, as it returned it.
///   </para>
/// </remarks>
public sealed class ForgeHook
{
  #region Fields

  /// <summary>The maximum length of <see cref="ScopeExternalId" />.</summary>
  public const int ScopeExternalIdMaxLength = 256;

  /// <summary>The maximum length of <see cref="ExternalId" />.</summary>
  public const int ExternalIdMaxLength = 128;

  /// <summary>The maximum length of <see cref="LastError" />; a longer error is cut.</summary>
  public const int LastErrorMaxLength = 1000;

  #endregion

  #region Ctors

  /// <summary>Initializes a new instance of the <see cref="ForgeHook" /> class.</summary>
  /// <param name="connectionId">The identifier of the connection that owns the hook.</param>
  /// <param name="scopeExternalId">The identifier the forge gives the scope, in lower case.</param>
  private ForgeHook(string connectionId, string scopeExternalId)
  {
    ConnectionId = connectionId;
    ScopeExternalId = scopeExternalId;
  }

  #endregion

  #region Properties

  /// <summary>Gets the identifier of the hook in the database, zero until it is saved.</summary>
  public long Id { get; private set; }

  /// <summary>Gets the identifier of the connection that owns the hook.</summary>
  public string ConnectionId { get; private set; }

  /// <summary>Gets what the hook covers.</summary>
  public ForgeHookScopeKind ScopeKind { get; private set; }

  /// <summary>Gets the identifier the forge gives the account, the project or the repository, in lower case.</summary>
  public string ScopeExternalId { get; private set; }

  /// <summary>
  ///   Gets the identifier the forge gives the hook, or <see langword="null" /> when no hook of the scope is known at
  ///   the forge.
  /// </summary>
  public string? ExternalId { get; private set; }

  /// <summary>Gets the state of the hook, as last checked.</summary>
  public ForgeHookState State { get; private set; }

  /// <summary>Gets when the hook was last checked against its forge, in UTC.</summary>
  public DateTimeOffset LastCheckedAt { get; private set; }

  /// <summary>Gets why the hook is disabled or forbidden, or <see langword="null" /> when the forge gave no reason.</summary>
  public string? LastError { get; private set; }

  #endregion

  #region Methods

  /// <summary>Records the hook of a scope that has none at its forge yet.</summary>
  /// <param name="connectionId">The identifier of the connection that owns the hook.</param>
  /// <param name="scopeKind">What the hook covers.</param>
  /// <param name="scopeExternalId">The identifier the forge gives the scope; kept in lower case.</param>
  /// <param name="at">The time of the check; converted to UTC.</param>
  /// <returns>The hook, <see cref="ForgeHookState.Missing" />, not saved yet.</returns>
  /// <exception cref="ArgumentNullException">
  ///   <paramref name="connectionId" /> or <paramref name="scopeExternalId" /> is <see langword="null" />.
  /// </exception>
  /// <exception cref="ArgumentOutOfRangeException"><paramref name="scopeKind" /> is not defined.</exception>
  /// <exception cref="ArgumentException">
  ///   <paramref name="connectionId" /> is not well formed, or <paramref name="scopeExternalId" /> is empty, white space
  ///   or too long.
  /// </exception>
  public static ForgeHook Create(string connectionId, ForgeHookScopeKind scopeKind, string scopeExternalId, DateTimeOffset at)
  {
    ArgumentNullException.ThrowIfNull(connectionId);
    ArgumentException.ThrowIf(!ForgeConnection.IsWellFormedId(connectionId), $"The connection identifier '{connectionId}' is not well formed.", nameof(connectionId));
    ArgumentOutOfRangeException.ThrowIfUndefined(scopeKind);
    ArgumentException.ThrowIfNullOrWhiteSpace(scopeExternalId);
    ThrowIfTooLong(scopeExternalId, ScopeExternalIdMaxLength, nameof(scopeExternalId));

    return new ForgeHook(connectionId, scopeExternalId.ToLowerInvariant())
    {
      ScopeKind = scopeKind,
      State = ForgeHookState.Missing,
      LastCheckedAt = at.ToUniversalTime(),
    };
  }

  /// <summary>Records that the hook exists at the forge and delivers its events.</summary>
  /// <param name="externalId">The identifier the forge gives the hook.</param>
  /// <param name="at">The time of the check; converted to UTC.</param>
  /// <exception cref="ArgumentNullException"><paramref name="externalId" /> is <see langword="null" />.</exception>
  /// <exception cref="ArgumentException"><paramref name="externalId" /> is empty, white space or too long.</exception>
  public void MarkActive(string externalId, DateTimeOffset at) => Record(ForgeHookState.Active, RequireExternalId(externalId), null, at);

  /// <summary>Records that the hook exists at the forge but is turned off.</summary>
  /// <param name="externalId">The identifier the forge gives the hook.</param>
  /// <param name="error">Why the forge turned it off, or <see langword="null" />; cut to <see cref="LastErrorMaxLength" />.</param>
  /// <param name="at">The time of the check; converted to UTC.</param>
  /// <exception cref="ArgumentNullException"><paramref name="externalId" /> is <see langword="null" />.</exception>
  /// <exception cref="ArgumentException"><paramref name="externalId" /> is empty, white space or too long.</exception>
  public void MarkDisabled(string externalId, string? error, DateTimeOffset at) => Record(ForgeHookState.Disabled, RequireExternalId(externalId), error, at);

  /// <summary>Records that the hook no longer exists at the forge; its identifier is forgotten.</summary>
  /// <param name="at">The time of the check; converted to UTC.</param>
  public void MarkMissing(DateTimeOffset at) => Record(ForgeHookState.Missing, null, null, at);

  /// <summary>Records that the credential of the connection has no right to read or create the hook.</summary>
  /// <param name="error">Why the forge refused; cut to <see cref="LastErrorMaxLength" />.</param>
  /// <param name="at">The time of the check; converted to UTC.</param>
  /// <remarks>The identifier of a hook known before stays, since the forge no longer tells whether it still exists.</remarks>
  /// <exception cref="ArgumentNullException"><paramref name="error" /> is <see langword="null" />.</exception>
  /// <exception cref="ArgumentException"><paramref name="error" /> is empty or white space.</exception>
  public void MarkForbidden(string error, DateTimeOffset at)
  {
    ArgumentException.ThrowIfNullOrWhiteSpace(error);

    Record(ForgeHookState.Forbidden, ExternalId, error, at);
  }

  /// <summary>Checks the identifier the forge gives a hook.</summary>
  /// <param name="externalId">The identifier.</param>
  /// <returns>The same identifier.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="externalId" /> is <see langword="null" />.</exception>
  /// <exception cref="ArgumentException"><paramref name="externalId" /> is empty, white space or too long.</exception>
  private static string RequireExternalId(string externalId)
  {
    ArgumentException.ThrowIfNullOrWhiteSpace(externalId);
    ThrowIfTooLong(externalId, ExternalIdMaxLength, nameof(externalId));

    return externalId;
  }

  /// <summary>Checks that a text is not longer than its column.</summary>
  /// <param name="value">The text.</param>
  /// <param name="maxLength">The maximum length.</param>
  /// <param name="name">The name of the parameter.</param>
  /// <exception cref="ArgumentException"><paramref name="value" /> is longer than <paramref name="maxLength" />.</exception>
  private static void ThrowIfTooLong(string value, int maxLength, string name) =>
    ArgumentException.ThrowIf(value.Length > maxLength, $"The value cannot be longer than {maxLength} characters.", name);

  /// <summary>Records the result of a check of the hook.</summary>
  /// <param name="state">The state found.</param>
  /// <param name="externalId">The identifier of the hook, or <see langword="null" />.</param>
  /// <param name="error">The reason, or <see langword="null" />; a blank reason is none, a long one is cut.</param>
  /// <param name="at">The time of the check; converted to UTC.</param>
  private void Record(ForgeHookState state, string? externalId, string? error, DateTimeOffset at)
  {
    State = state;
    ExternalId = externalId;
    LastError = string.IsNullOrWhiteSpace(error) ? null : error[..Math.Min(error.Length, LastErrorMaxLength)];
    LastCheckedAt = at.ToUniversalTime();
  }

  #endregion
}
