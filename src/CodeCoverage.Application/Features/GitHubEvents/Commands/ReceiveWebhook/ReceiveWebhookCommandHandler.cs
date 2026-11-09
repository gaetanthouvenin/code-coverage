// ------------------------------------------------------------------------------------------------
// <copyright file="ReceiveWebhookCommandHandler.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Collections.Frozen;
using System.Globalization;
using System.Text.Json.Nodes;

using Mediator;

using CodeCoverage.Application.Exceptions;
using CodeCoverage.Domain.Contracts.GitHub;
using CodeCoverage.Domain.Contracts.Jobs;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Domain.Repositories;

namespace CodeCoverage.Application.Features.GitHubEvents.Commands.ReceiveWebhook;

/// <summary>Handler for the <see cref="ReceiveWebhookCommand" /> command.</summary>
/// <param name="signatures">The validator of the signature.</param>
/// <param name="installations">The installations of the GitHub App.</param>
/// <param name="deliveries">The webhook deliveries.</param>
/// <param name="clock">The clock.</param>
/// <remarks>
///   <para>
///     The secret of the App is the same for every organization: the signature proves that GitHub sent the body, and
///     the <c>installation.id</c> of that signed body then names the organization. It is read once, from a global table,
///     before any scope has entered an organization, so that the job is queued by
///     <see cref="JobRequest.ForOrganization" /> (ADR-013). Nothing here calls GitHub.
///   </para>
///   <para>
///     An organization that is not active tracks nothing: only the events that can change its state are kept, on the
///     lane of the instance.
///   </para>
/// </remarks>
internal sealed class ReceiveWebhookCommandHandler(
  IWebhookSignatureValidator signatures,
  IInstallationRepository installations,
  IWebhookDeliveryRepository deliveries,
  IClock clock) : ICommandHandler<ReceiveWebhookCommand, WebhookOutcome>
{
  #region Fields

  private const string InvalidSignatureMessage = "The signature of the delivery is missing or invalid.";

  private const string InvalidPayloadMessage = "The body of the delivery is not a JSON object GitHub would send.";

  /// <summary>The events kept for an organization that is not active, or for an installation not known yet.</summary>
  private static readonly FrozenSet<string> InstanceEvents =
    new[] { "installation", "installation_repositories", "installation_target" }.ToFrozenSet(StringComparer.Ordinal);

  #endregion

  #region Methods

  /// <summary>Builds the job of a delivery.</summary>
  /// <param name="delivery">The delivery.</param>
  /// <param name="owner">The owner of its installation, or <see langword="null" />.</param>
  /// <returns>The job, or <see langword="null" /> when the delivery is dropped.</returns>
  private static JobRequest? ToJob(GitHubWebhookEvent delivery, InstallationOwner? owner)
  {
    var payload = new JsonObject { ["deliveryId"] = delivery.DeliveryId }.ToJsonString();
    var lockKey = ToLockKey(delivery);

    if (owner is { State: OrganizationState.Active })
    {
      return JobRequest.ForOrganization(owner.OrganizationId, JobTypes.HandleWebhook, payload, lockKey);
    }

    return InstanceEvents.Contains(delivery.Event) ? JobRequest.ForInstance(JobTypes.HandleWebhook, payload, lockKey) : null;
  }

  /// <summary>Gives the key that serializes the deliveries of one repository or of one installation.</summary>
  /// <param name="delivery">The delivery.</param>
  /// <returns>The key, or <see langword="null" /> when the delivery names neither.</returns>
  private static string? ToLockKey(GitHubWebhookEvent delivery) =>
    delivery switch
    {
      { RepositoryId: { } repository } => string.Create(CultureInfo.InvariantCulture, $"repo:{repository}"),
      { InstallationId: { } installation } => string.Create(CultureInfo.InvariantCulture, $"installation:{installation}"),
      var _ => null,
    };

  #endregion

  #region Implementation of ICommandHandler<ReceiveWebhookCommand,WebhookOutcome>

  /// <inheritdoc />
  /// <exception cref="UnauthorizedException">The signature is missing or matches no secret.</exception>
  /// <exception cref="BadRequestException">The body is not a JSON object GitHub would send.</exception>
  public async ValueTask<WebhookOutcome> Handle(ReceiveWebhookCommand command, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(command);

    if (!signatures.IsValid(command.Payload, command.Signature))
    {
      throw new UnauthorizedException(GitHubEventErrors.InvalidSignature, InvalidSignatureMessage);
    }

    var delivery = GitHubWebhookEvent.TryParse(command.DeliveryId, command.Event, command.Payload)
                   ?? throw new BadRequestException(GitHubEventErrors.InvalidPayload, InvalidPayloadMessage);

    var owner = delivery.InstallationId is { } installationId
      ? await installations.FindOwnerAsync(installationId, cancellationToken).ConfigureAwait(false)
      : null;

    var job = ToJob(delivery, owner);

    if (job is null)
    {
      return WebhookOutcome.Dropped;
    }

    return await deliveries.TryStoreAsync(delivery, job, clock.UtcNow, cancellationToken).ConfigureAwait(false)
      ? WebhookOutcome.Accepted
      : WebhookOutcome.Duplicate;
  }

  #endregion
}
