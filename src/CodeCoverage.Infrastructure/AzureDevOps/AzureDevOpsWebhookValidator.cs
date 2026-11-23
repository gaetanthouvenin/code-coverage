// ------------------------------------------------------------------------------------------------
// <copyright file="AzureDevOpsWebhookValidator.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Security.Cryptography;
using System.Text;

using CodeCoverage.Domain.Contracts.Webhooks;
using CodeCoverage.Domain.Ports;

namespace CodeCoverage.Infrastructure.AzureDevOps;

/// <summary>Authenticates an Azure DevOps service hook by the secret header of its connection (ADR-014).</summary>
/// <remarks>
///   <para>
///     Registered as the <see cref="IForgeWebhookValidator" /> keyed by
///     <see cref="Domain.Organizations.Forge.AzureDevOps" />. Azure DevOps does not sign its service hooks: each
///     subscription sends the webhook secret of its connection in <see cref="AzureDevOpsWebhookHeaders.Secret" />. The
///     header is compared with the current secret and the previous one, through their SHA-256 digests and
///     <see cref="CryptographicOperations.FixedTimeEquals" />, so that neither the content nor the length of a secret
///     shows in the time of the answer.
///   </para>
///   <para>Nothing is called on Azure DevOps, and the body is not read.</para>
/// </remarks>
internal sealed class AzureDevOpsWebhookValidator : IForgeWebhookValidator
{
  #region Methods

  /// <summary>Gives the SHA-256 digest of a text, so that two texts compare in a time that ignores their lengths.</summary>
  /// <param name="value">The text.</param>
  /// <returns>The digest of its UTF-8 bytes.</returns>
  private static byte[] Digest(string value) => SHA256.HashData(Encoding.UTF8.GetBytes(value));

  #endregion

  #region Implementation of IForgeWebhookValidator

  /// <inheritdoc />
  public bool IsValid(ForgeWebhookRequest request, IReadOnlyList<string> secrets)
  {
    ArgumentNullException.ThrowIfNull(request);
    ArgumentNullException.ThrowIfNull(secrets);

    if (!request.Headers.TryGetValue(AzureDevOpsWebhookHeaders.Secret, out var header) || string.IsNullOrEmpty(header))
    {
      return false;
    }

    var received = Digest(header);
    var valid = false;

    // Every secret is compared, even after a match: the time does not tell which one matched.
    foreach (var secret in secrets)
    {
      valid |= !string.IsNullOrEmpty(secret) && CryptographicOperations.FixedTimeEquals(received, Digest(secret));
    }

    return valid;
  }

  #endregion
}
