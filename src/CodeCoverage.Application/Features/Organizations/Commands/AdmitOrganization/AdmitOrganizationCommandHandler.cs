// ------------------------------------------------------------------------------------------------
// <copyright file="AdmitOrganizationCommandHandler.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using CodeCoverage.Application.Exceptions;
using CodeCoverage.Domain.Contracts.Jobs;
using CodeCoverage.Domain.Operations;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Ports;

namespace CodeCoverage.Application.Features.Organizations.Commands.AdmitOrganization;

/// <summary>Handler for the <see cref="AdmitOrganizationCommand" /> command.</summary>
/// <param name="directory">The directory of the organizations.</param>
/// <param name="currentUser">The principal that admits the organization.</param>
/// <param name="clock">The clock.</param>
/// <param name="auditLog">The audit trail.</param>
/// <param name="jobQueue">The job queue.</param>
internal sealed class AdmitOrganizationCommandHandler(
  IOrganizationDirectory directory,
  ICurrentUser currentUser,
  IClock clock,
  IAuditLog auditLog,
  IJobQueue jobQueue) : ICommandHandler<AdmitOrganizationCommand, Organization>
{
  #region Implementation of ICommandHandler<AdmitOrganizationCommand,Organization>

  /// <inheritdoc />
  /// <exception cref="UnprocessableEntityException">The login is reserved.</exception>
  /// <exception cref="ConflictException">The organization was already admitted or is suspended.</exception>
  /// <exception cref="NotFoundException">The organization disappeared while it was admitted.</exception>
  public async ValueTask<Organization> Handle(AdmitOrganizationCommand command, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(command);

    if (ReservedLogins.IsReserved(command.Login))
    {
      throw new UnprocessableEntityException(OrganizationErrors.ReservedLogin, $"The login '{command.Login}' cannot be admitted.");
    }

    var existing = await directory.FindByAccountIdAsync(command.Forge, command.ExternalAccountId, cancellationToken).ConfigureAwait(false);

    if (existing is { State: not OrganizationState.Pending })
    {
      throw new ConflictException(OrganizationErrors.NotPending, $"The organization is {existing.State}: only a pending one can be admitted.");
    }

    // A new account is taken for an organization: the next synchronization with the forge corrects a personal one.
    var accountType = existing?.AccountType ?? OrganizationAccountType.Organization;
    var pending = await directory.UpsertFromForgeAsync(command.Forge, command.ExternalAccountId, command.Login, accountType, cancellationToken)
                                 .ConfigureAwait(false);

    var actor = currentUser.GetActorName();
    var now = clock.UtcNow;

    var admitted = await directory.ChangeAsync(command.Forge, pending.ExternalAccountId, organization => organization.Admit(actor, now), cancellationToken)
                                  .ConfigureAwait(false);

    if (admitted is null)
    {
      throw new NotFoundException(OrganizationErrors.NotFound, "The organization no longer exists.");
    }

    await auditLog.RecordAsync("Organization.Admit", $"{command.Forge}/{admitted.Login}", null, cancellationToken).ConfigureAwait(false);

    // The admission knows no installation: the coordinator lists them and queues the listing of the repositories of
    // this organization, now that it is active. The admission is saved already; the daily reconciliation does the
    // same if this job is never queued. Only GitHub has installations to list.
    if (command.Forge is Forge.GitHub)
    {
      await jobQueue.EnqueueAsync(SyncInstallationsJobs.Create(), cancellationToken).ConfigureAwait(false);
    }

    return admitted;
  }

  #endregion
}
