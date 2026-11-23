// ------------------------------------------------------------------------------------------------
// <copyright file="AzureDevOpsWebhookMapper.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Text.Json;

using Mediator;

using CodeCoverage.Application.Features.Webhooks.Commands.ReceiveForgeWebhook;
using CodeCoverage.Domain.Contracts.GitHub;
using CodeCoverage.Domain.Contracts.Webhooks;
using CodeCoverage.Domain.Organizations;

namespace CodeCoverage.Api.Mapping;

/// <summary>Converts the service hooks of Azure DevOps between the Domain and the API.</summary>
internal static class AzureDevOpsWebhookMapper
{
  #region Methods

  /// <summary>Receives a service hook of a connection and gives the status of the answer.</summary>
  /// <param name="sender">The request sender.</param>
  /// <param name="connectionId">The identifier of the connection, read from the address of the route.</param>
  /// <param name="requestHeaders">
  ///   The headers of the request, of which only <see cref="AzureDevOpsWebhookHeaders.Secret" /> is given to the
  ///   validator of the forge.
  /// </param>
  /// <param name="payload">The raw body.</param>
  /// <param name="cancellationToken">A token to cancel the request.</param>
  /// <returns>200 for a delivery already known, otherwise 202.</returns>
  /// <exception cref="ArgumentNullException">
  ///   <paramref name="sender" />, <paramref name="requestHeaders" /> or <paramref name="payload" /> is
  ///   <see langword="null" />.
  /// </exception>
  /// <remarks>
  ///   Azure DevOps sends neither the identifier of the delivery nor the event in a header: they are the <c>id</c> and
  ///   the <c>eventType</c> of the body, read here only to name the delivery. Nothing else of the body is believed, and
  ///   the connection is never read from it (ADR-017). A body that does not carry them is refused by the validation of
  ///   the command.
  /// </remarks>
  public static async Task<int> ReceiveAzureDevOpsWebhookAsync(
    this ISender sender,
    string connectionId,
    IHeaderDictionary requestHeaders,
    byte[] payload,
    CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(sender);
    ArgumentNullException.ThrowIfNull(requestHeaders);
    ArgumentNullException.ThrowIfNull(payload);

    var (deliveryId, @event) = ReadEnvelope(payload);
    Dictionary<string, string> headers = new(StringComparer.OrdinalIgnoreCase);

    if (requestHeaders[AzureDevOpsWebhookHeaders.Secret].FirstOrDefault() is { } secret)
    {
      headers[AzureDevOpsWebhookHeaders.Secret] = secret;
    }

    var command = new ReceiveForgeWebhookCommand(Forge.AzureDevOps, connectionId, deliveryId, @event, headers, payload);
    var outcome = await sender.Send(command, cancellationToken).ConfigureAwait(false);

    return outcome is WebhookOutcome.Duplicate ? StatusCodes.Status200OK : StatusCodes.Status202Accepted;
  }

  /// <summary>Reads the identifier of the event and its type from the body of a service hook.</summary>
  /// <param name="payload">The raw body.</param>
  /// <returns>
  ///   The <c>id</c> and the <c>eventType</c> of the body; each is empty when the body is not a JSON object or does not
  ///   carry it as a text.
  /// </returns>
  private static (string DeliveryId, string Event) ReadEnvelope(byte[] payload)
  {
    try
    {
      using var document = JsonDocument.Parse(payload);
      var root = document.RootElement;

      return (ReadText(root, "id"), ReadText(root, "eventType"));
    }
    catch (JsonException)
    {
      return (string.Empty, string.Empty);
    }
  }

  /// <summary>Reads a member of an object that holds a text.</summary>
  /// <param name="element">The element, which may not be an object.</param>
  /// <param name="name">The name of the member.</param>
  /// <returns>The text, or an empty text when the element is not an object or its member is not a text.</returns>
  private static string ReadText(JsonElement element, string name) =>
    element.ValueKind is JsonValueKind.Object && element.TryGetProperty(name, out var value) && value.ValueKind is JsonValueKind.String
      ? value.GetString()!
      : string.Empty;

  #endregion
}
