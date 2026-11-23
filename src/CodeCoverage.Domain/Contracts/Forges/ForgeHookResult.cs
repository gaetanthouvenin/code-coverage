// ------------------------------------------------------------------------------------------------
// <copyright file="ForgeHookResult.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Connections;

namespace CodeCoverage.Domain.Contracts.Forges;

/// <summary>The state a hook manager found, or left, for the hook of one scope at its forge.</summary>
/// <remarks>
///   A refusal of the forge is a result, <see cref="ForgeHookState.Forbidden" />, not a failure: only a forge that cannot be
///   reached fails the call.
/// </remarks>
public sealed record ForgeHookResult
{
  #region Ctors

  /// <summary>Initializes a new instance of the <see cref="ForgeHookResult" /> class.</summary>
  /// <param name="scope">The scope of the hook.</param>
  /// <param name="state">The state of the hook.</param>
  /// <param name="hookExternalId">The identifier the forge gives the hook, or <see langword="null" />.</param>
  /// <param name="error">Why the hook is disabled or forbidden, or <see langword="null" />.</param>
  private ForgeHookResult(ForgeHookScope scope, ForgeHookState state, string? hookExternalId, string? error)
  {
    Scope = scope;
    State = state;
    HookExternalId = hookExternalId;
    Error = error;
  }

  #endregion

  #region Properties

  /// <summary>Gets the scope of the hook.</summary>
  public ForgeHookScope Scope { get; }

  /// <summary>Gets the state of the hook.</summary>
  public ForgeHookState State { get; }

  /// <summary>
  ///   Gets the identifier the forge gives the hook, set for <see cref="ForgeHookState.Active" /> and
  ///   <see cref="ForgeHookState.Disabled" />, otherwise <see langword="null" />.
  /// </summary>
  public string? HookExternalId { get; }

  /// <summary>
  ///   Gets why the hook is disabled or forbidden, never a secret, set for <see cref="ForgeHookState.Forbidden" />, or
  ///   <see langword="null" /> when the forge gave no reason.
  /// </summary>
  public string? Error { get; }

  #endregion

  #region Methods

  /// <summary>Builds the result of a hook that exists at the forge and delivers its events.</summary>
  /// <param name="scope">The scope of the hook.</param>
  /// <param name="hookExternalId">The identifier the forge gives the hook.</param>
  /// <returns>The result, <see cref="ForgeHookState.Active" />.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="scope" /> or <paramref name="hookExternalId" /> is <see langword="null" />.</exception>
  /// <exception cref="ArgumentException"><paramref name="hookExternalId" /> is empty or white space.</exception>
  public static ForgeHookResult Active(ForgeHookScope scope, string hookExternalId)
  {
    ArgumentNullException.ThrowIfNull(scope);
    ArgumentException.ThrowIfNullOrWhiteSpace(hookExternalId);

    return new ForgeHookResult(scope, ForgeHookState.Active, hookExternalId, null);
  }

  /// <summary>Builds the result of a hook that exists at the forge but is turned off.</summary>
  /// <param name="scope">The scope of the hook.</param>
  /// <param name="hookExternalId">The identifier the forge gives the hook.</param>
  /// <param name="error">Why the forge turned it off, never a secret, or <see langword="null" />.</param>
  /// <returns>The result, <see cref="ForgeHookState.Disabled" />.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="scope" /> or <paramref name="hookExternalId" /> is <see langword="null" />.</exception>
  /// <exception cref="ArgumentException"><paramref name="hookExternalId" /> is empty or white space.</exception>
  public static ForgeHookResult Disabled(ForgeHookScope scope, string hookExternalId, string? error)
  {
    ArgumentNullException.ThrowIfNull(scope);
    ArgumentException.ThrowIfNullOrWhiteSpace(hookExternalId);

    return new ForgeHookResult(scope, ForgeHookState.Disabled, hookExternalId, error);
  }

  /// <summary>Builds the result of a scope that has no hook at the forge.</summary>
  /// <param name="scope">The scope.</param>
  /// <returns>The result, <see cref="ForgeHookState.Missing" />.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="scope" /> is <see langword="null" />.</exception>
  public static ForgeHookResult Missing(ForgeHookScope scope)
  {
    ArgumentNullException.ThrowIfNull(scope);

    return new ForgeHookResult(scope, ForgeHookState.Missing, null, null);
  }

  /// <summary>Builds the result of a scope whose hook the credential of the connection has no right to read or create.</summary>
  /// <param name="scope">The scope.</param>
  /// <param name="error">Why the forge refused, never a secret.</param>
  /// <returns>The result, <see cref="ForgeHookState.Forbidden" />.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="scope" /> or <paramref name="error" /> is <see langword="null" />.</exception>
  /// <exception cref="ArgumentException"><paramref name="error" /> is empty or white space.</exception>
  public static ForgeHookResult Forbidden(ForgeHookScope scope, string error)
  {
    ArgumentNullException.ThrowIfNull(scope);
    ArgumentException.ThrowIfNullOrWhiteSpace(error);

    return new ForgeHookResult(scope, ForgeHookState.Forbidden, null, error);
  }

  #endregion
}
