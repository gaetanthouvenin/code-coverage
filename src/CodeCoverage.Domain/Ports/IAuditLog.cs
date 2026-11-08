// ------------------------------------------------------------------------------------------------
// <copyright file="IAuditLog.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Operations;

namespace CodeCoverage.Domain.Ports;

/// <summary>
///   The audit trail of the sensitive actions: tokens issued or revoked, settings changed, secrets
///   replaced and manual reprocessing.
/// </summary>
/// <remarks>
///   The actor is the principal of the current scope (<see cref="ICurrentUser" />): a user, the system
///   or nobody. The caller never names it, so that an entry cannot be attributed to someone else.
/// </remarks>
public interface IAuditLog
{
  #region Methods

  /// <summary>Records an action, saved at once.</summary>
  /// <param name="action">What was done, as <c>Feature.Verb</c> (for example <c>Settings.Update</c>).</param>
  /// <param name="target">What the action applies to (for example the key of a setting).</param>
  /// <param name="details">
  ///   The context of the action, or <see langword="null" /> for none. Never a secret value: a key
  ///   that contains <c>Secret</c> is refused.
  /// </param>
  /// <param name="cancellationToken">A token to cancel the saving.</param>
  /// <returns>A task that completes when the entry is saved.</returns>
  /// <exception cref="ArgumentNullException">
  ///   <paramref name="action" /> or <paramref name="target" /> is <see langword="null" />.
  /// </exception>
  /// <exception cref="ArgumentException">
  ///   <paramref name="action" /> or <paramref name="target" /> is empty, white space or too long, or
  ///   <paramref name="details" /> holds a key that contains <c>Secret</c>.
  /// </exception>
  /// <seealso cref="AuditEntry" />
  Task RecordAsync(
    string action,
    string target,
    IReadOnlyDictionary<string, string>? details,
    CancellationToken cancellationToken);

  #endregion
}
