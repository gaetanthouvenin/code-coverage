// ------------------------------------------------------------------------------------------------
// <copyright file="CheckConnectionCommandHandler.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using CodeCoverage.Domain.Connections;
using CodeCoverage.Domain.Contracts.Forges;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Ports;

namespace CodeCoverage.Application.Features.Connections.Commands.CheckConnection;

/// <summary>Handler for the <see cref="CheckConnectionCommand" /> command.</summary>
/// <param name="connections">The repository of the forge connections.</param>
/// <param name="forges">Resolves the connection verifier of the forge.</param>
/// <param name="protector">The secret protector, which gives back the credential for the time of the verification.</param>
/// <param name="directory">The directory of the organizations, which gives the login of the organization of the scope.</param>
/// <param name="organization">The organization of the scope.</param>
/// <param name="clock">The clock.</param>
/// <remarks>
///   <para>
///     The connection is read from a global table: one of another organization, an unknown one or a suspended one is
///     left alone. The check never suspends a connection: it records <see cref="ForgeConnectionStatus.Failing" /> when
///     the forge refuses the credential, when it reaches another account or when the forge cannot be reached, and
///     <see cref="ForgeConnectionStatus.Expiring" /> when the credential expires within <see cref="ExpiryWarning" />,
///     judged on the expiry the forge reports at the check when it reports one.
///   </para>
///   <para>
///     A failed check is a result, not an error of the job: it is recorded and the job ends, so that it is not retried
///     and fails no other job. Only a cancellation stops it.
///   </para>
/// </remarks>
internal sealed class CheckConnectionCommandHandler(
  IForgeConnectionRepository connections,
  IForgeGatewayProvider forges,
  ISecretProtector protector,
  IOrganizationDirectory directory,
  ICurrentOrganization organization,
  IClock clock) : ICommandHandler<CheckConnectionCommand>
{
  #region Properties

  /// <summary>Gets how long before its expiry a connection whose check succeeds is marked expiring.</summary>
  public static TimeSpan ExpiryWarning { get; } = TimeSpan.FromDays(30);

  #endregion

  #region Methods

  /// <summary>Verifies a connection against its forge.</summary>
  /// <param name="connection">The connection of the organization of the scope.</param>
  /// <param name="login">The login of the organization of the connection.</param>
  /// <param name="cancellationToken">A token to cancel the verification.</param>
  /// <returns>
  ///   Why the check failed, never a secret, or <see langword="null" /> when the forge accepts the connection; and, when
  ///   it accepts it, the expiry of the credential the forge reports, or <see langword="null" /> when it reports none.
  /// </returns>
  private async Task<(string? Error, DateTimeOffset? ExpiresAt)> VerifyAsync(ForgeConnection connection, string login, CancellationToken cancellationToken)
  {
    ForgeConnectionCheck check;

    try
    {
      var request = new ForgeConnectionRequest(connection.Forge, connection.Kind, login, connection.TenantId, protector.Unprotect(connection.CredentialProtected));

      check = await forges.GetVerifier(connection.Forge).VerifyAsync(request, cancellationToken).ConfigureAwait(false);
    }
    catch (Exception exception) when (exception is not OperationCanceledException)
    {
      // The message of the exception is not kept: it could carry what the forge answered about the credential.
      return ($"The check could not reach the forge ({exception.GetType().Name}).", null);
    }

    if (check.IsRefused)
    {
      return (check.Refusal, null);
    }

    var reached = Organization.NormalizeExternalAccountId(check.Account.ExternalAccountId);

    return string.Equals(reached, connection.ExternalAccountId, StringComparison.Ordinal)
      ? (null, check.Account.ExpiresAt)
      : ("The credential reaches another account.", null);
  }

  #endregion

  #region Implementation of ICommandHandler<CheckConnectionCommand>

  /// <inheritdoc />
  public async ValueTask<Unit> Handle(CheckConnectionCommand command, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(command);

    var connection = await connections.FindAsync(command.ConnectionId, cancellationToken).ConfigureAwait(false);

    // The job names the connection in its payload: only the one of this organization is ever checked here.
    if (connection is null || connection.OrganizationId != organization.Id || connection.Status is ForgeConnectionStatus.Suspended)
    {
      return Unit.Value;
    }

    var current = await directory.FindCurrentAsync(cancellationToken).ConfigureAwait(false);

    if (current is null)
    {
      return Unit.Value;
    }

    var (error, expiresAt) = await VerifyAsync(connection, current.Login, cancellationToken).ConfigureAwait(false);

    // The expiry the forge reports at each check wins over the one known at admission or rotation.
    connection.RecordCheck(clock.UtcNow, error, ExpiryWarning, expiresAt);
    await connections.UpdateAsync(connection, cancellationToken).ConfigureAwait(false);

    return Unit.Value;
  }

  #endregion
}
