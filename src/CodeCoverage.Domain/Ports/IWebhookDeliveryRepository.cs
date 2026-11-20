// ------------------------------------------------------------------------------------------------
// <copyright file="IWebhookDeliveryRepository.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Contracts.Forges;
using CodeCoverage.Domain.Contracts.GitHub;
using CodeCoverage.Domain.Contracts.Jobs;
using CodeCoverage.Domain.Operations;
using CodeCoverage.Domain.Organizations;

namespace CodeCoverage.Domain.Ports;

/// <summary>The webhook deliveries received from the forges.</summary>
/// <remarks>
///   <para>An operational table, never filtered: the reception runs before a scope enters an organization.</para>
///   <para>
///     A delivery is known by its forge and its identifier: the same identifier on two forges designates two
///     deliveries. Storing works on the deliveries of GitHub, whose connection is the installation; reading and marking
///     have a form for GitHub and one for the forges with connections; the listing gives the deliveries of every forge.
///   </para>
/// </remarks>
public interface IWebhookDeliveryRepository
{
  #region Methods

  /// <summary>Stores a GitHub delivery and queues its job in one transaction.</summary>
  /// <param name="delivery">The delivery; its installation and its repository are stored in text.</param>
  /// <param name="job">The job that handles it; its lane gives the organization of the row.</param>
  /// <param name="at">The current time; converted to UTC.</param>
  /// <param name="cancellationToken">A token to cancel the saving.</param>
  /// <returns><see langword="true" /> when the delivery is new; <see langword="false" /> when GitHub already sent it.</returns>
  /// <exception cref="ArgumentNullException">An argument is <see langword="null" />.</exception>
  /// <exception cref="ArgumentException"><paramref name="job" /> names no organization and is not on the lane of the instance.</exception>
  Task<bool> TryStoreAsync(GitHubWebhookEvent delivery, JobRequest job, DateTimeOffset at, CancellationToken cancellationToken);

  /// <summary>Reads a GitHub delivery to handle it, when it belongs to the organization of the scope or to none.</summary>
  /// <param name="deliveryId">The identifier of the delivery on GitHub.</param>
  /// <param name="cancellationToken">A token to cancel the reading.</param>
  /// <returns>The delivery, or <see langword="null" />.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="deliveryId" /> is <see langword="null" />.</exception>
  Task<GitHubWebhookEvent?> FindForProcessingAsync(string deliveryId, CancellationToken cancellationToken);

  /// <summary>Records that a GitHub delivery was handled, when it belongs to the organization of the scope or to none.</summary>
  /// <param name="deliveryId">The identifier of the delivery on GitHub.</param>
  /// <param name="at">The current time; converted to UTC.</param>
  /// <param name="cancellationToken">A token to cancel the saving.</param>
  /// <returns>A task that completes when the date is saved; a delivery handled again keeps its first date.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="deliveryId" /> is <see langword="null" />.</exception>
  Task MarkProcessedAsync(string deliveryId, DateTimeOffset at, CancellationToken cancellationToken);

  /// <summary>
  ///   Reads a delivery of a forge with connections to handle it, when it belongs to the organization of the scope or
  ///   to none.
  /// </summary>
  /// <param name="forge">The forge that sent the delivery.</param>
  /// <param name="deliveryId">The identifier of the delivery on its forge.</param>
  /// <param name="cancellationToken">A token to cancel the reading.</param>
  /// <returns>The delivery, or <see langword="null" />, also when it names no connection.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="deliveryId" /> is <see langword="null" />.</exception>
  Task<ForgeDelivery?> FindForgeDeliveryForProcessingAsync(Forge forge, string deliveryId, CancellationToken cancellationToken);

  /// <summary>Records that a delivery of a forge was handled, when it belongs to the organization of the scope or to none.</summary>
  /// <param name="forge">The forge that sent the delivery.</param>
  /// <param name="deliveryId">The identifier of the delivery on its forge.</param>
  /// <param name="at">The current time; converted to UTC.</param>
  /// <param name="cancellationToken">A token to cancel the saving.</param>
  /// <returns>A task that completes when the date is saved; a delivery handled again keeps its first date.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="deliveryId" /> is <see langword="null" />.</exception>
  Task MarkForgeDeliveryProcessedAsync(Forge forge, string deliveryId, DateTimeOffset at, CancellationToken cancellationToken);

  /// <summary>Lists the latest deliveries of every forge, without their bodies.</summary>
  /// <param name="count">How many deliveries to list.</param>
  /// <param name="cancellationToken">A token to cancel the reading.</param>
  /// <returns>The deliveries, the latest first.</returns>
  /// <exception cref="ArgumentOutOfRangeException"><paramref name="count" /> is not positive.</exception>
  Task<IReadOnlyList<WebhookDeliverySummary>> ListRecentAsync(int count, CancellationToken cancellationToken);

  #endregion
}
