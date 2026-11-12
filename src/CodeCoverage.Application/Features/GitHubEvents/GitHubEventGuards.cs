// ------------------------------------------------------------------------------------------------
// <copyright file="GitHubEventGuards.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Diagnostics.CodeAnalysis;

using CodeCoverage.Application.Exceptions;
using CodeCoverage.Domain.Contracts.GitHub;

namespace CodeCoverage.Application.Features.GitHubEvents;

/// <summary>
///   The refusals of the GitHub events, in the form of
///   <see cref="ArgumentNullException.ThrowIfNull(object?, string?)" />: one place holds the code and the message of each.
/// </summary>
internal static class GitHubEventGuards
{
  #region Methods

  extension(UnauthorizedException)
  {
    /// <summary>Throws when the signature of a delivery is missing or matches no secret.</summary>
    /// <param name="valid">Whether the signature is valid.</param>
    /// <exception cref="UnauthorizedException"><paramref name="valid" /> is <see langword="false" />.</exception>
    public static void ThrowIfWebhookSignatureInvalid([DoesNotReturnIf(false)] bool valid)
    {
      if (!valid)
      {
        throw new UnauthorizedException(GitHubEventErrors.InvalidSignature, "The signature of the delivery is missing or invalid.");
      }
    }
  }

  extension(BadRequestException)
  {
    /// <summary>Throws when the body of a delivery is not a JSON object GitHub would send.</summary>
    /// <param name="delivery">The parsed delivery, or <see langword="null" />.</param>
    /// <exception cref="BadRequestException"><paramref name="delivery" /> is <see langword="null" />.</exception>
    public static void ThrowIfWebhookPayloadInvalid([NotNull] GitHubWebhookEvent? delivery)
    {
      if (delivery is null)
      {
        throw new BadRequestException(GitHubEventErrors.InvalidPayload, "The body of the delivery is not a JSON object GitHub would send.");
      }
    }
  }

  extension(NotFoundException)
  {
    /// <summary>Throws when the delivery to handle is not stored, or belongs to another organization.</summary>
    /// <param name="delivery">The delivery, or <see langword="null" />.</param>
    /// <exception cref="NotFoundException"><paramref name="delivery" /> is <see langword="null" />.</exception>
    public static void ThrowIfWebhookDeliveryNotFound([NotNull] GitHubWebhookEvent? delivery)
    {
      if (delivery is null)
      {
        throw new NotFoundException(GitHubEventErrors.DeliveryNotFound, "The delivery to handle does not exist.");
      }
    }
  }

  #endregion
}
