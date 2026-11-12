// ------------------------------------------------------------------------------------------------
// <copyright file="IOrganizationDirectory.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Organizations;

namespace CodeCoverage.Domain.Ports;

/// <summary>The directory of the organizations hosted by the instance.</summary>
/// <remarks>
///   The directory reads a global table without business data: it serves before a scope enters an
///   organization. A login or an account identifier only means something on its forge.
/// </remarks>
public interface IOrganizationDirectory
{
  #region Methods

  /// <summary>Finds an organization by its identifier, whatever its state.</summary>
  /// <param name="id">The identifier of the organization.</param>
  /// <param name="cancellationToken">A token to cancel the reading.</param>
  /// <returns>The organization, or <see langword="null" />.</returns>
  Task<Organization?> FindAsync(long id, CancellationToken cancellationToken);

  /// <summary>Finds an organization by the current login of its account, whatever its case.</summary>
  /// <param name="forge">The forge of the account.</param>
  /// <param name="login">The login.</param>
  /// <param name="cancellationToken">A token to cancel the reading.</param>
  /// <returns>
  ///   The organization, or <see langword="null" /> when no account of this forge holds this login or when
  ///   it is not a well-formed login, which is always the case for a tombstone.
  /// </returns>
  /// <exception cref="ArgumentNullException"><paramref name="login" /> is <see langword="null" />.</exception>
  Task<Organization?> FindByLoginAsync(Forge forge, string login, CancellationToken cancellationToken);

  /// <summary>Finds an organization by the identifier the forge gives its account.</summary>
  /// <param name="forge">The forge of the account.</param>
  /// <param name="externalAccountId">The identifier of the account, whatever its case.</param>
  /// <param name="cancellationToken">A token to cancel the reading.</param>
  /// <returns>The organization, or <see langword="null" />.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="externalAccountId" /> is <see langword="null" />.</exception>
  Task<Organization?> FindByAccountIdAsync(Forge forge, string externalAccountId, CancellationToken cancellationToken);

  /// <summary>Finds the organization the current scope has entered, whatever its state.</summary>
  /// <param name="cancellationToken">A token to cancel the reading.</param>
  /// <returns>The organization, or <see langword="null" /> when its row no longer exists.</returns>
  /// <exception cref="InvalidOperationException">The scope has not entered an organization.</exception>
  Task<Organization?> FindCurrentAsync(CancellationToken cancellationToken);

  /// <summary>Lists the active organizations of every forge.</summary>
  /// <param name="cancellationToken">A token to cancel the reading.</param>
  /// <returns>The active organizations, by identifier.</returns>
  Task<IReadOnlyList<Organization>> ListActiveAsync(CancellationToken cancellationToken);

  /// <summary>Lists every organization of every forge, whatever its state, for the administrators of the instance.</summary>
  /// <param name="cancellationToken">A token to cancel the reading.</param>
  /// <returns>The organizations, by identifier, for reading only.</returns>
  Task<IReadOnlyList<Organization>> ListAllAsync(CancellationToken cancellationToken);

  /// <summary>Creates or updates the organization of a forge account, saved at once.</summary>
  /// <param name="forge">The forge of the account.</param>
  /// <param name="externalAccountId">The identifier the forge gives the account.</param>
  /// <param name="login">The login that the forge reports for the account.</param>
  /// <param name="accountType">The kind of account that the forge reports.</param>
  /// <param name="cancellationToken">A token to cancel the saving.</param>
  /// <returns>The organization of the account; a new one is pending.</returns>
  /// <remarks>
  ///   When another account of the same forge still holds this login, its row gets a tombstone in the same
  ///   transaction: the login then resolves to this account only. Admission never happens here.
  /// </remarks>
  /// <exception cref="ArgumentNullException">
  ///   <paramref name="externalAccountId" /> or <paramref name="login" /> is <see langword="null" />.
  /// </exception>
  /// <exception cref="ArgumentException">
  ///   <paramref name="externalAccountId" /> is empty, white space or too long, or <paramref name="login" />
  ///   is not a well-formed login.
  /// </exception>
  Task<Organization> UpsertFromForgeAsync(
    Forge forge,
    string externalAccountId,
    string login,
    OrganizationAccountType accountType,
    CancellationToken cancellationToken);

  /// <summary>Applies a change to the organization of a forge account, saved at once.</summary>
  /// <param name="forge">The forge of the account.</param>
  /// <param name="externalAccountId">The identifier of the account, whatever its case.</param>
  /// <param name="change">What changes in the organization.</param>
  /// <param name="cancellationToken">A token to cancel the saving.</param>
  /// <returns>The organization as saved, or <see langword="null" /> when no such account exists.</returns>
  /// <remarks>The change runs again on a fresh row when another save won the race.</remarks>
  /// <exception cref="ArgumentNullException">
  ///   <paramref name="externalAccountId" /> or <paramref name="change" /> is <see langword="null" />.
  /// </exception>
  Task<Organization?> ChangeAsync(
    Forge forge,
    string externalAccountId,
    Action<Organization> change,
    CancellationToken cancellationToken);

  #endregion
}
