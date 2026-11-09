// ------------------------------------------------------------------------------------------------
// <copyright file="GitHubEventMapper.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using CodeCoverage.Application.Features.GitHubEvents.Commands.ReceiveWebhook;
using CodeCoverage.Domain.Contracts.GitHub;

namespace CodeCoverage.Api.Mapping;

/// <summary>Converts the webhooks of GitHub between the Domain and the API.</summary>
internal static class GitHubEventMapper
{
  #region Methods

  /// <summary>Receives a webhook and gives the status of the answer.</summary>
  /// <param name="sender">The request sender.</param>
  /// <param name="deliveryId">The identifier of the delivery.</param>
  /// <param name="event">The name of the event.</param>
  /// <param name="signature">The signature, or <see langword="null" />.</param>
  /// <param name="payload">The raw body.</param>
  /// <param name="cancellationToken">A token to cancel the request.</param>
  /// <returns>200 for a delivery already known, otherwise 202.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="sender" /> is <see langword="null" />.</exception>
  public static async Task<int> ReceiveGitHubWebhookAsync(
    this ISender sender,
    string deliveryId,
    string @event,
    string? signature,
    byte[] payload,
    CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(sender);

    var outcome = await sender.Send(new ReceiveWebhookCommand(deliveryId, @event, signature, payload), cancellationToken).ConfigureAwait(false);

    return outcome is WebhookOutcome.Duplicate ? StatusCodes.Status200OK : StatusCodes.Status202Accepted;
  }

  #endregion
}
