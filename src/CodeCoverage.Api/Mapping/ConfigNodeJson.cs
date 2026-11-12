// ------------------------------------------------------------------------------------------------
// <copyright file="ConfigNodeJson.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Text.Json;
using System.Text.Json.Nodes;

using CodeCoverage.Api.Configuration;
using CodeCoverage.Domain.Config;

namespace CodeCoverage.Api.Mapping;

/// <summary>Writes a configuration tree as JSON, for the answers of the API.</summary>
internal static class ConfigNodeJson
{
  #region Fields

  private static readonly JsonSerializerOptions Options = new() { MaxDepth = PresentationConfiguration.MaximumJsonDepth };

  #endregion

  #region Methods

  /// <summary>Writes a configuration tree as a JSON element.</summary>
  /// <param name="node">The tree.</param>
  /// <returns>The element.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="node" /> is <see langword="null" />.</exception>
  public static JsonElement ToJsonElement(ConfigNode node)
  {
    ArgumentNullException.ThrowIfNull(node);

    return JsonSerializer.SerializeToElement(ToJson(node), Options);
  }

  private static JsonNode? ToJson(ConfigNode node) =>
    node.Kind switch
    {
      ConfigNodeKind.Map => new JsonObject(node.Entries.Select(static e => KeyValuePair.Create(e.Key, ToJson(e.Value)))),
      ConfigNodeKind.List => new JsonArray([.. node.Items.Select(ToJson)]),
      ConfigNodeKind.Boolean => JsonValue.Create(node.AsBoolean()),
      ConfigNodeKind.Number => JsonValue.Create(node.AsNumber()),
      ConfigNodeKind.Text => JsonValue.Create(node.AsText()),
      var _ => null,
    };

  #endregion
}
