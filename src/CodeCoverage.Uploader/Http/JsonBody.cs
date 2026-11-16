// ------------------------------------------------------------------------------------------------
// <copyright file="JsonBody.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Text.Json;

using CodeCoverage.Uploader.Serialization;

namespace CodeCoverage.Uploader.Http;

/// <summary>Reads the properties of a JSON body the server answered, such as the <c>errorCode</c> of its problem details.</summary>
internal static class JsonBody
{
  #region Methods

  /// <summary>Reads a text property of a JSON body.</summary>
  /// <param name="body">The body.</param>
  /// <param name="name">The name of the property.</param>
  /// <returns>The value, or <see langword="null" /> when the body is not a JSON object or has no such text property.</returns>
  public static string? ReadString(string body, string name)
  {
    try
    {
      var json = JsonSerializer.Deserialize(body, UploaderJsonContext.Default.JsonElement);

      return json.ValueKind is JsonValueKind.Object && json.TryGetProperty(name, out var value) && value.ValueKind is JsonValueKind.String
               ? value.GetString()
               : null;
    }
    catch (JsonException)
    {
      return null;
    }
  }

  #endregion
}
