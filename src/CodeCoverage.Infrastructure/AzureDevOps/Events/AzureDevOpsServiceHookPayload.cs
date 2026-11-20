// ------------------------------------------------------------------------------------------------
// <copyright file="AzureDevOpsServiceHookPayload.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Globalization;
using System.Text.Json;

namespace CodeCoverage.Infrastructure.AzureDevOps.Events;

/// <summary>The body of an Azure DevOps service hook, read without trusting its shape.</summary>
/// <remarks>
///   <para>
///     The subscriptions ask for <c>resourceDetailsToSend=Minimal</c> (ADR-014): Azure DevOps sends only the key fields
///     of the resource, and which ones depends on the event and on its <c>resourceVersion</c>. The body is therefore read
///     field by field, never through a model whose required members would refuse a field Azure DevOps left out: an
///     absent or malformed field reads as <see langword="null" />, and the translator decides whether the event can do
///     without it.
///   </para>
///   <para>Nothing read here is believed: the state is always read again from the API.</para>
/// </remarks>
internal sealed class AzureDevOpsServiceHookPayload
{
  #region Ctors

  /// <summary>Initializes a new instance of the <see cref="AzureDevOpsServiceHookPayload" /> class.</summary>
  /// <param name="root">The root object of the body, detached from its document.</param>
  /// <param name="resource">The <c>resource</c> object of the body.</param>
  private AzureDevOpsServiceHookPayload(JsonElement root, JsonElement resource)
  {
    Resource = resource;
    MessageText = GetObject(root, "message") is { } message ? GetString(message, "text") : null;
    AccountId = GetObject(root, "resourceContainers") is { } containers && GetObject(containers, "account") is { } account
                  ? ToIdentifier(GetString(account, "id"))
                  : null;
  }

  #endregion

  #region Properties

  /// <summary>Gets the <c>resource</c> object of the body: the push, the pull request or the repository.</summary>
  public JsonElement Resource { get; }

  /// <summary>Gets the text of the <c>message</c> of the body, or <see langword="null" /> when it was not sent.</summary>
  public string? MessageText { get; }

  /// <summary>
  ///   Gets the identifier of the Azure DevOps organization, read from <c>resourceContainers.account.id</c> and written
  ///   as an identifier of the port, or <see langword="null" /> when the body does not carry it.
  /// </summary>
  public string? AccountId { get; }

  #endregion

  #region Methods

  /// <summary>Reads the body of a service hook.</summary>
  /// <param name="body">The raw body.</param>
  /// <returns>The body, or <see langword="null" /> when it is not a JSON object with a <c>resource</c> object.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="body" /> is <see langword="null" />.</exception>
  public static AzureDevOpsServiceHookPayload? Read(byte[] body)
  {
    ArgumentNullException.ThrowIfNull(body);

    try
    {
      using var document = JsonDocument.Parse(body);
      var root = document.RootElement.Clone();

      return GetObject(root, "resource") is { } resource ? new AzureDevOpsServiceHookPayload(root, resource) : null;
    }
    catch (JsonException)
    {
      return null;
    }
  }

  /// <summary>Reads a member of an object that holds an object.</summary>
  /// <param name="element">The element, which may not be an object.</param>
  /// <param name="name">The name of the member.</param>
  /// <returns>The object, or <see langword="null" /> when the element is not an object or its member is not one.</returns>
  public static JsonElement? GetObject(JsonElement element, string name) => GetMember(element, name, JsonValueKind.Object);

  /// <summary>Reads a member of an object that holds an array.</summary>
  /// <param name="element">The element, which may not be an object.</param>
  /// <param name="name">The name of the member.</param>
  /// <returns>The array, or <see langword="null" /> when the element is not an object or its member is not one.</returns>
  public static JsonElement? GetArray(JsonElement element, string name) => GetMember(element, name, JsonValueKind.Array);

  /// <summary>Reads a member of an object that holds a text.</summary>
  /// <param name="element">The element, which may not be an object.</param>
  /// <param name="name">The name of the member.</param>
  /// <returns>The text, or <see langword="null" /> when the element is not an object or its member is not a text.</returns>
  public static string? GetString(JsonElement element, string name) =>
    GetMember(element, name, JsonValueKind.String) is { } value ? value.GetString() : null;

  /// <summary>Reads a member of an object that holds a positive whole number.</summary>
  /// <param name="element">The element, which may not be an object.</param>
  /// <param name="name">The name of the member.</param>
  /// <returns>The number, or <see langword="null" /> when the member is not a whole number above zero.</returns>
  public static int? GetPositiveInt32(JsonElement element, string name) =>
    GetMember(element, name, JsonValueKind.Number) is { } value && value.TryGetInt32(out var number) && number > 0 ? number : null;

  /// <summary>Writes an identifier of Azure DevOps as the text the port carries.</summary>
  /// <param name="value">The identifier as the body writes it, or <see langword="null" />.</param>
  /// <returns>The identifier in lower case and without braces, or <see langword="null" /> when the text is not one.</returns>
  public static string? ToIdentifier(string? value) =>
    Guid.TryParse(value, out var id) ? id.ToString("D", CultureInfo.InvariantCulture) : null;

  /// <summary>Reads a member of an object, when it is of the expected kind.</summary>
  /// <param name="element">The element, which may not be an object.</param>
  /// <param name="name">The name of the member.</param>
  /// <param name="kind">The kind the member must be of.</param>
  /// <returns>The member, or <see langword="null" />.</returns>
  private static JsonElement? GetMember(JsonElement element, string name, JsonValueKind kind) =>
    element.ValueKind is JsonValueKind.Object && element.TryGetProperty(name, out var value) && value.ValueKind == kind ? value : null;

  #endregion
}
