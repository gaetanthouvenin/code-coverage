// ------------------------------------------------------------------------------------------------
// <copyright file="WebhookGuards.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Diagnostics.CodeAnalysis;

using CodeCoverage.Application.Exceptions;
using CodeCoverage.Domain.Connections;
using CodeCoverage.Domain.Contracts.Forges;

namespace CodeCoverage.Application.Features.Webhooks;

/// <summary>
///   The refusals of the webhooks of the forges with connections, in the form of
///   <see cref="ArgumentNullException.ThrowIfNull(object?, string?)" />: one place holds the code and the message of each.
/// </summary>
internal static class WebhookGuards
{
  #region Methods

  extension(UnauthorizedException)
  {
    /// <summary>Throws when the connection of a delivery is unknown or no secret of it authenticates the delivery.</summary>
    /// <param name="owner">The owner of the connection that authenticated the delivery, or <see langword="null" />.</param>
    /// <exception cref="UnauthorizedException"><paramref name="owner" /> is <see langword="null" />.</exception>
    /// <remarks>One code and one message for both cases: the route is not an oracle of the connections (ADR-017).</remarks>
    public static void ThrowIfForgeWebhookUnauthenticated([NotNull] ForgeConnectionOwner? owner)
    {
      if (owner is null)
      {
        throw new UnauthorizedException(
          WebhookErrors.InvalidSignature,
          "The connection is unknown or the signature of the delivery is missing or invalid."
        );
      }
    }
  }

  extension(BadRequestException)
  {
    /// <summary>Throws when the body of a delivery is not a JSON object.</summary>
    /// <param name="valid">Whether the body is a JSON object.</param>
    /// <exception cref="BadRequestException"><paramref name="valid" /> is <see langword="false" />.</exception>
    public static void ThrowIfForgeWebhookPayloadInvalid([DoesNotReturnIf(false)] bool valid)
    {
      if (!valid)
      {
        throw new BadRequestException(WebhookErrors.InvalidPayload, "The body of the delivery is not a JSON object.");
      }
    }
  }

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
