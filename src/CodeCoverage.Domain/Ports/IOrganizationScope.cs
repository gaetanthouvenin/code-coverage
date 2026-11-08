// ------------------------------------------------------------------------------------------------
// <copyright file="IOrganizationScope.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Organizations;

namespace CodeCoverage.Domain.Ports;

/// <summary>Makes the current DI scope enter an organization, from the address a caller gave.</summary>
/// <remarks>
///   The entry takes a login and never an identifier, so that the Application cannot choose an
///   organization that an address does not name.
/// </remarks>
public interface IOrganizationScope
{
  #region Methods

  /// <summary>Enters the active organization that holds a login on a forge.</summary>
  /// <param name="forge">The forge of the account.</param>
  /// <param name="login">The login, whatever its case, as received from the caller.</param>
  /// <param name="cancellationToken">A token to cancel the reading.</param>
  /// <returns>
  ///   <see langword="true" /> when the scope has entered the organization; <see langword="false" />, with
  ///   the scope left as it was, when <paramref name="login" /> is <see langword="null" />, empty or
  ///   malformed, or when no active organization of this forge holds it.
  /// </returns>
  /// <exception cref="InvalidOperationException">The scope has already entered another organization.</exception>
  Task<bool> TryEnterAsync(Forge forge, string? login, CancellationToken cancellationToken);

  #endregion
}
