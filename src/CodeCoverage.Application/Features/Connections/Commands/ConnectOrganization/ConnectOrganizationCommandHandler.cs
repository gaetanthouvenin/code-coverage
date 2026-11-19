// ------------------------------------------------------------------------------------------------
// <copyright file="ConnectOrganizationCommandHandler.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using CodeCoverage.Application.Exceptions;
using CodeCoverage.Domain.Connections;
using CodeCoverage.Domain.Contracts.Forges;
using CodeCoverage.Domain.Contracts.Jobs;
using CodeCoverage.Domain.Operations;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Ports;

namespace CodeCoverage.Application.Features.Connections.Commands.ConnectOrganization;

/// <summary>Handler for the <see cref="ConnectOrganizationCommand" /> command.</summary>
/// <param name="forges">Resolves the connection verifier of the forge.</param>
/// <param name="connections">The repository of the forge connections, which saves the organization with its connection.</param>
/// <param name="protector">The secret protector, which protects the credential and the secret of the webhooks.</param>
/// <param name="tokenGenerator">Generates the secret of the webhooks of the connection.</param>
/// <param name="currentUser">The principal that admits the organization.</param>
/// <param name="clock">The clock.</param>
/// <param name="auditLog">The audit trail.</param>
/// <remarks>
///   The one way to admit an account of a forge with connections, which Bitbucket and GitLab reuse: the forge verifies
///   the access, then the organization, admitted, its connection and the jobs that follow them are saved in one
///   transaction by <see cref="IForgeConnectionRepository.AddWithOrganizationAsync" />. No connection is saved without
///   its organization, nor the reverse.
/// </remarks>
internal sealed class ConnectOrganizationCommandHandler(
  IForgeGatewayProvider forges,
  IForgeConnectionRepository connections,
  ISecretProtector protector,
  ITokenGenerator tokenGenerator,
  ICurrentUser currentUser,
  IClock clock,
  IAuditLog auditLog) : ICommandHandler<ConnectOrganizationCommand, Organization>
{
  #region Methods

  /// <summary>Gives the identifier the instance gives a new connection, never the forge.</summary>
  /// <param name="forge">The forge of the connection, which starts the identifier.</param>
  /// <returns>The segment of the forge and a random part, well formed for <see cref="ForgeConnection.IsWellFormedId" />.</returns>
  private static string NewConnectionId(Forge forge) => $"{ForgeSegments.ToSegment(forge)}-{Guid.NewGuid():N}";

  /// <summary>Builds the jobs that follow a new connection, once its organization has its identifier.</summary>
  /// <param name="organization">The organization, saved.</param>
  /// <param name="connectionId">The identifier of the connection.</param>
  /// <param name="userId">The administrator who admits the organization, or <see langword="null" /> for the system.</param>
  /// <returns>
  ///   The synchronization of the repositories of the connection, in the organization, and the refresh of the
  ///   memberships of the administrator, on the lane of the instance, so that it sees the organization without signing in
  ///   again.
  /// </returns>
  private static List<JobRequest> JobsOf(Organization organization, string connectionId, long? userId)
  {
    var key = RepositoryReconciliationJobs.ConnectionKey(connectionId);
    var jobs = new List<JobRequest>
    {
      JobRequest.ForOrganization(
        organization.Id,
        JobTypes.SyncConnectionRepositories,
        RepositoryReconciliationJobs.ConnectionPayload(organization.Forge, connectionId),
        key,
        $"sync:{key}",
        coalesce: true
      ),
    };

    if (userId is { } administrator)
    {
      jobs.Add(RefreshUserMembershipsJobs.Create(administrator));
    }

    return jobs;
  }

  #endregion

  #region Implementation of ICommandHandler<ConnectOrganizationCommand,Organization>

  /// <inheritdoc />
  /// <exception cref="UnprocessableEntityException">The forge refuses the connection access to the account.</exception>
  /// <exception cref="ConflictException">An organization of that account or login on that forge exists already.</exception>
  public async ValueTask<Organization> Handle(ConnectOrganizationCommand command, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(command);

    var request = new ForgeConnectionRequest(command.Forge, command.Kind, command.Login, command.TenantId, command.Credential);
    var check = await forges.GetVerifier(command.Forge).VerifyAsync(request, cancellationToken).ConfigureAwait(false);

    UnprocessableEntityException.ThrowIfForgeAccessDenied(check.IsRefused, check.Refusal, command.Forge, command.Login);

    var account = check.Account;
    var baseAddress = check.BaseAddress;
    var now = clock.UtcNow;

    // A new account is taken for an organization: the next synchronization with the forge corrects it.
    var organization = Organization.CreatePending(command.Forge, account.ExternalAccountId, command.Login, OrganizationAccountType.Organization, now);
    organization.Admit(currentUser.GetActorName(), now);

    // The plain values never leave this method: only their protected forms reach the connection.
    var credentialProtected = protector.Protect(command.Credential ?? string.Empty);
    var webhookSecretProtected = protector.Protect(tokenGenerator.NewWebhookSecret());
    var connectionId = NewConnectionId(command.Forge);
    var userId = currentUser.UserId;

    var connection = await connections.AddWithOrganizationAsync(
                                        organization,
                                        saved => ForgeConnection.Create(
                                          connectionId,
                                          saved.Id,
                                          saved.Forge,
                                          command.Kind,
                                          account.ExternalAccountId,
                                          baseAddress,
                                          command.TenantId,
                                          account.ServiceAccountExternalId,
                                          credentialProtected,
                                          webhookSecretProtected,
                                          account.ExpiresAt,
                                          now
                                        ),
                                        saved => JobsOf(saved, connectionId, userId),
                                        cancellationToken
                                      )
                                      .ConfigureAwait(false);

    var details = new Dictionary<string, string>(StringComparer.Ordinal) { ["ConnectionId"] = connection.Id };

    await auditLog.RecordAsync("Connection.Connect", $"{command.Forge}/{organization.Login}", details, cancellationToken).ConfigureAwait(false);

    return organization;
  }

  #endregion
}
