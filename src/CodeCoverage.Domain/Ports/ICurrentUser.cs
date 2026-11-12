// ------------------------------------------------------------------------------------------------
// <copyright file="ICurrentUser.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Domain.Ports;

/// <summary>
///   Who the current request, circuit or job runs for: a signed-in user, the system itself, or
///   nobody.
/// </summary>
/// <remarks>
///   The system principal is produced only by the worker and the webhook processing; the HTTP hosts
///   can never produce it.
/// </remarks>
public interface ICurrentUser
{
  #region Properties

  /// <summary>
  ///   Gets the identifier of the signed-in user, or <see langword="null" /> when nobody is
  ///   signed in or when the work runs for the system.
  /// </summary>
  long? UserId { get; }

  /// <summary>
  ///   Gets the GitHub login of the signed-in user, or <see langword="null" /> when nobody is
  ///   signed in or when the work runs for the system.
  /// </summary>
  /// <remarks>
  ///   The administration command line is the one system principal with a value here: the bracketed
  ///   marker it leaves in the audit trail, which no forge login can be.
  /// </remarks>
  string? Login { get; }

  /// <summary>Gets a value indicating whether a user is signed in.</summary>
  bool IsAuthenticated { get; }

  /// <summary>
  ///   Gets a value indicating whether the signed-in user is an administrator of the instance.
  /// </summary>
  bool IsAdmin { get; }

  /// <summary>
  ///   Gets a value indicating whether the work runs for the application itself (jobs, webhooks),
  ///   which passes every authorization check. Never true for an HTTP request or a circuit.
  /// </summary>
  bool IsSystem { get; }

  /// <summary>
  ///   Gets the organization of the personal token of the API that authenticates the request, or
  ///   <see langword="null" /> when no token of an organization does: a signed-in user on the Web, a token of the
  ///   instance, the system.
  /// </summary>
  /// <remarks>Such a request enters this organization only: the gate refuses any other as unknown.</remarks>
  long? TokenOrganizationId { get; }

  /// <summary>
  ///   Gets a value indicating whether a personal token of the instance authenticates the request: it enters no
  ///   organization.
  /// </summary>
  bool IsInstanceToken { get; }

  #endregion
}
