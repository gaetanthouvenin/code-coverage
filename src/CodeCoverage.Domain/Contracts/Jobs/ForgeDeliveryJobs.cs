// ------------------------------------------------------------------------------------------------
// <copyright file="ForgeDeliveryJobs.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Text.Json;

using CodeCoverage.Domain.Guards;
using CodeCoverage.Domain.Organizations;

namespace CodeCoverage.Domain.Contracts.Jobs;

/// <summary>Builds what the <see cref="JobTypes.HandleForgeDelivery" /> job carries.</summary>
/// <remarks>
///   The job is queued by the reception of the delivery, which names its organization: only the payload is shared
///   here, so that the job dispatcher reads what the reception writes.
/// </remarks>
public static class ForgeDeliveryJobs
{
  #region Methods

  /// <summary>Builds the payload of the handling of a delivery of a forge with connections.</summary>
  /// <param name="forge">The forge that sent the delivery, never GitHub, whose deliveries keep their own job.</param>
  /// <param name="deliveryId">The identifier of the delivery on its forge.</param>
  /// <returns>The payload.</returns>
  /// <exception cref="ArgumentException">
  ///   <paramref name="deliveryId" /> is empty or white space, or <paramref name="forge" /> is GitHub.
  /// </exception>
  /// <exception cref="ArgumentNullException"><paramref name="deliveryId" /> is <see langword="null" />.</exception>
  /// <exception cref="ArgumentOutOfRangeException"><paramref name="forge" /> is not a known forge.</exception>
  public static string Payload(Forge forge, string deliveryId)
  {
    ArgumentOutOfRangeException.ThrowIfUndefined(forge);
    ArgumentException.ThrowIf(forge is Forge.GitHub, "A delivery of GitHub is handled by its own job.", nameof(forge));
    ArgumentException.ThrowIfNullOrWhiteSpace(deliveryId);

    return JsonSerializer.Serialize(new { Forge = forge.ToString(), DeliveryId = deliveryId });
  }

  #endregion
}
