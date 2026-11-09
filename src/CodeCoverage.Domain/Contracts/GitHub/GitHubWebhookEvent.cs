// ------------------------------------------------------------------------------------------------
// <copyright file="GitHubWebhookEvent.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Text.Json;

namespace CodeCoverage.Domain.Contracts.GitHub;

/// <summary>A webhook delivery of GitHub: its headers, the fields that route it, and its raw body.</summary>
public sealed class GitHubWebhookEvent
{
  #region Fields

  /// <summary>The maximum length of <see cref="DeliveryId" />; GitHub sends a GUID.</summary>
  public const int DeliveryIdMaxLength = 64;

  /// <summary>The maximum length of <see cref="Event" />.</summary>
  public const int EventMaxLength = 64;

  /// <summary>The maximum length of <see cref="Action" />.</summary>
  public const int ActionMaxLength = 64;

  #endregion

  #region Ctors

  /// <summary>Initializes a new instance of the <see cref="GitHubWebhookEvent" /> class.</summary>
  /// <param name="deliveryId">The identifier of the delivery.</param>
  /// <param name="event">The name of the event.</param>
  /// <param name="action">The action of the event, or <see langword="null" />.</param>
  /// <param name="installationId">The installation, or <see langword="null" />.</param>
  /// <param name="repositoryId">The identifier of the repository on GitHub, or <see langword="null" />.</param>
  /// <param name="payload">The raw body.</param>
  public GitHubWebhookEvent(string deliveryId, string @event, string? action, long? installationId, long? repositoryId, byte[] payload)
  {
    ArgumentNullException.ThrowIfNull(deliveryId);
    ArgumentNullException.ThrowIfNull(@event);
    ArgumentNullException.ThrowIfNull(payload);

    DeliveryId = deliveryId;
    Event = @event;
    Action = action;
    InstallationId = installationId;
    RepositoryId = repositoryId;
    Payload = payload;
  }

  #endregion

  #region Properties

  /// <summary>Gets the identifier of the delivery (<c>X-GitHub-Delivery</c>), the same when GitHub delivers it again.</summary>
  public string DeliveryId { get; }

  /// <summary>Gets the name of the event (<c>X-GitHub-Event</c>).</summary>
  public string Event { get; }

  /// <summary>Gets the action of the event, or <see langword="null" /> for an event without one.</summary>
  public string? Action { get; }

  /// <summary>Gets the identifier of the installation of the GitHub App, or <see langword="null" />.</summary>
  public long? InstallationId { get; }

  /// <summary>Gets the identifier of the repository on GitHub, or <see langword="null" />.</summary>
  public long? RepositoryId { get; }

  /// <summary>Gets the raw body of the delivery, as it was signed.</summary>
  public byte[] Payload { get; }

  #endregion

  #region Methods

  /// <summary>Reads the fields that route a delivery from its body.</summary>
  /// <param name="deliveryId">The identifier of the delivery.</param>
  /// <param name="event">The name of the event.</param>
  /// <param name="payload">The raw body.</param>
  /// <returns>The delivery, or <see langword="null" /> when the body is not a JSON object or its action does not fit.</returns>
  /// <exception cref="ArgumentNullException">An argument is <see langword="null" />.</exception>
  public static GitHubWebhookEvent? TryParse(string deliveryId, string @event, byte[] payload)
  {
    ArgumentNullException.ThrowIfNull(deliveryId);
    ArgumentNullException.ThrowIfNull(@event);
    ArgumentNullException.ThrowIfNull(payload);

    try
    {
      using var document = JsonDocument.Parse(payload);
      var root = document.RootElement;

      if (root.ValueKind is not JsonValueKind.Object)
      {
        return null;
      }

      var action = root.TryGetProperty("action", out var value) && value.ValueKind is JsonValueKind.String ? value.GetString() : null;

      return action?.Length > ActionMaxLength
        ? null
        : new GitHubWebhookEvent(deliveryId, @event, action, ReadId(root, "installation"), ReadId(root, "repository"), payload);
    }
    catch (Exception exception) when (exception is JsonException or InvalidOperationException)
    {
      return null;
    }
  }

  /// <summary>Reads the positive identifier of a nested object.</summary>
  /// <param name="root">The body.</param>
  /// <param name="name">The name of the nested object.</param>
  /// <returns>The identifier, or <see langword="null" />.</returns>
  private static long? ReadId(JsonElement root, string name) =>
    root.TryGetProperty(name, out var owner)
    && owner.ValueKind is JsonValueKind.Object
    && owner.TryGetProperty("id", out var id)
    && id.ValueKind is JsonValueKind.Number
    && id.TryGetInt64(out var value)
    && value > 0
      ? value
      : null;

  #endregion
}
