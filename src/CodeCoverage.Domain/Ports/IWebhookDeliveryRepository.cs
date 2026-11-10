// ------------------------------------------------------------------------------------------------
// <copyright file="IWebhookDeliveryRepository.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Contracts.GitHub;
using CodeCoverage.Domain.Contracts.Jobs;
using CodeCoverage.Domain.Operations;

namespace CodeCoverage.Domain.Ports;

/// <summary>The webhook deliveries received from GitHub.</summary>
/// <remarks>An operational table, never filtered: the reception runs before a scope enters an organization.</remarks>
public interface IWebhookDeliveryRepository
{
  #region Methods

  /// <summary>Stores a delivery and queues its job in one transaction.</summary>
  /// <param name="delivery">The delivery.</param>
  /// <param name="job">The job that handles it; its lane gives the organization of the row.</param>
  /// <param name="at">The current time; converted to UTC.</param>
  /// <param name="cancellationToken">A token to cancel the saving.</param>
  /// <returns><see langword="true" /> when the delivery is new; <see langword="false" /> when it was already known.</returns>
  /// <exception cref="ArgumentNullException">An argument is <see langword="null" />.</exception>
  /// <exception cref="ArgumentException"><paramref name="job" /> names no organization and is not on the lane of the instance.</exception>
  Task<bool> TryStoreAsync(GitHubWebhookEvent delivery, JobRequest job, DateTimeOffset at, CancellationToken cancellationToken);

  /// <summary>Reads a delivery to handle it, when it belongs to the organization of the scope or to none.</summary>
  /// <param name="deliveryId">The identifier of the delivery.</param>
  /// <param name="cancellationToken">A token to cancel the reading.</param>
  /// <returns>The delivery, or <see langword="null" />.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="deliveryId" /> is <see langword="null" />.</exception>
  Task<GitHubWebhookEvent?> FindForProcessingAsync(string deliveryId, CancellationToken cancellationToken);

  /// <summary>Records that a delivery was handled, when it belongs to the organization of the scope or to none.</summary>
  /// <param name="deliveryId">The identifier of the delivery.</param>
  /// <param name="at">The current time; converted to UTC.</param>
  /// <param name="cancellationToken">A token to cancel the saving.</param>
  /// <returns>A task that completes when the date is saved; a delivery handled again keeps its first date.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="deliveryId" /> is <see langword="null" />.</exception>
  Task MarkProcessedAsync(string deliveryId, DateTimeOffset at, CancellationToken cancellationToken);

  /// <summary>Lists the latest deliveries, without their bodies.</summary>
  /// <param name="count">How many deliveries to list.</param>
  /// <param name="cancellationToken">A token to cancel the reading.</param>
  /// <returns>The deliveries, the latest first.</returns>
  /// <exception cref="ArgumentOutOfRangeException"><paramref name="count" /> is not positive.</exception>
  Task<IReadOnlyList<WebhookDeliverySummary>> ListRecentAsync(int count, CancellationToken cancellationToken);

  #endregion
}
