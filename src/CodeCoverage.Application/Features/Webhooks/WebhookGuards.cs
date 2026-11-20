// ------------------------------------------------------------------------------------------------
// <copyright file="WebhookGuards.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Diagnostics.CodeAnalysis;

using CodeCoverage.Application.Exceptions;
using CodeCoverage.Domain.Contracts.Forges;

namespace CodeCoverage.Application.Features.Webhooks;

/// <summary>
///   The refusals of the webhooks of the forges with connections, in the form of
///   <see cref="ArgumentNullException.ThrowIfNull(object?, string?)" />: one place holds the code and the message of each.
/// </summary>
internal static class WebhookGuards
{
  #region Methods

  extension(NotFoundException)
  {
    /// <summary>Throws when the delivery to handle is not stored, names no connection, or belongs to another organization.</summary>
    /// <param name="delivery">The delivery, or <see langword="null" />.</param>
    /// <exception cref="NotFoundException"><paramref name="delivery" /> is <see langword="null" />.</exception>
    public static void ThrowIfForgeDeliveryNotFound([NotNull] ForgeDelivery? delivery)
    {
      if (delivery is null)
      {
        throw new NotFoundException(WebhookErrors.DeliveryNotFound, "The delivery to handle does not exist.");
      }
    }
  }

  #endregion
}
