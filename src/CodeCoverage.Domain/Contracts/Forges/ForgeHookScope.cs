// ------------------------------------------------------------------------------------------------
// <copyright file="ForgeHookScope.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Connections;
using CodeCoverage.Domain.Guards;

namespace CodeCoverage.Domain.Contracts.Forges;

/// <summary>What a hook of a forge connection covers, as the hook manager of its forge is asked to ensure or check it.</summary>
/// <remarks>
///   <para>Two scopes are the same scope when their kind is the same and their identifiers are equal, whatever their case.</para>
///   <para>
///     The identifier of a <see cref="ForgeHookScopeKind.Project" /> scope is the name of the project as tracked, the
///     namespace of its repositories, not an identifier of the forge: a tracked repository keeps no stable identifier of its
///     project, so the hook manager resolves the name to the project. Once a project is renamed, its scope carries the new
///     name and no hook last known; the row recorded under the old name is no longer refreshed.
///   </para>
/// </remarks>
public sealed record ForgeHookScope
{
  #region Ctors

  /// <summary>Initializes a new instance of the <see cref="ForgeHookScope" /> class.</summary>
  /// <param name="kind">What the hook covers.</param>
  /// <param name="externalId">
  ///   The identifier the forge gives the account or the repository, or the name of the project as tracked.
  /// </param>
  /// <param name="hookExternalId">
  ///   The identifier the forge gave the hook of the scope when it was last checked, or <see langword="null" /> when none
  ///   is known.
  /// </param>
  /// <exception cref="ArgumentOutOfRangeException"><paramref name="kind" /> is not defined.</exception>
  /// <exception cref="ArgumentNullException"><paramref name="externalId" /> is <see langword="null" />.</exception>
  /// <exception cref="ArgumentException"><paramref name="externalId" /> is empty or white space.</exception>
  public ForgeHookScope(ForgeHookScopeKind kind, string externalId, string? hookExternalId)
  {
    ArgumentOutOfRangeException.ThrowIfUndefined(kind);
    ArgumentException.ThrowIfNullOrWhiteSpace(externalId);

    Kind = kind;
    ExternalId = externalId;
    HookExternalId = string.IsNullOrWhiteSpace(hookExternalId) ? null : hookExternalId;
  }

  #endregion

  #region Properties

  /// <summary>Gets what the hook covers.</summary>
  public ForgeHookScopeKind Kind { get; }

  /// <summary>Gets the identifier the forge gives the account or the repository, or the name of the project as tracked.</summary>
  public string ExternalId { get; }

  /// <summary>Gets the identifier the forge gave the hook of the scope, or <see langword="null" /> when none is known.</summary>
  public string? HookExternalId { get; }

  #endregion

  #region Methods

  /// <summary>Tells whether another scope covers the same account, project or repository.</summary>
  /// <param name="other">The other scope.</param>
  /// <returns>
  ///   <see langword="true" /> when the kinds are the same and the identifiers are equal, whatever their case; otherwise
  ///   <see langword="false" />.
  /// </returns>
  /// <exception cref="ArgumentNullException"><paramref name="other" /> is <see langword="null" />.</exception>
  public bool IsSameScope(ForgeHookScope other)
  {
    ArgumentNullException.ThrowIfNull(other);

    return Kind == other.Kind && string.Equals(ExternalId, other.ExternalId, StringComparison.OrdinalIgnoreCase);
  }

  #endregion
}
