// ------------------------------------------------------------------------------------------------
// <copyright file="ConfigurationMapper.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Text.Json;
using System.Text.Json.Nodes;

using Mediator;

using CodeCoverage.Api.Configuration;
using CodeCoverage.Api.Contracts.Configuration;
using CodeCoverage.Application.Features.Configuration.Queries.ValidateCoverageYaml;
using CodeCoverage.Domain.Config;

namespace CodeCoverage.Api.Mapping;

/// <summary>Converts the coverage configuration between the Domain and the API.</summary>
internal static class ConfigurationMapper
{
  #region Fields

  private static readonly JsonSerializerOptions NormalizedOptions =
    new() { MaxDepth = PresentationConfiguration.MaximumJsonDepth };

  #endregion

  #region Methods

  /// <summary>Validates a <c>codecov.yml</c> file and converts the result for the API.</summary>
  /// <param name="sender">The request sender.</param>
  /// <param name="yaml">The text of the file.</param>
  /// <param name="cancellationToken">A token to cancel the request.</param>
  /// <returns>The response.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="sender" /> is <see langword="null" />.</exception>
  public static async Task<ValidationResultResponse> GetValidationResultResponseAsync(
    this ISender sender,
    string yaml,
    CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(sender);

    var result = await sender.Send(new ValidateCoverageYamlQuery(yaml), cancellationToken).ConfigureAwait(false);

    return result.ToResponse();
  }

  private static ValidationResultResponse ToResponse(this ConfigValidationResult result) =>
    new(
      result.IsValid,
      result.Normalized is { } normalized
        ? JsonSerializer.SerializeToElement(normalized.ToJson(), NormalizedOptions)
        : null,
      [.. result.Warnings.Select(w => new ConfigWarningModel(w.Code, w.Key, w.Detail))],
      result.Errors);

  private static JsonNode? ToJson(this ConfigNode node) =>
    node.Kind switch
    {
      ConfigNodeKind.Map => new JsonObject(node.Entries.Select(e => KeyValuePair.Create(e.Key, e.Value.ToJson()))),
      ConfigNodeKind.List => new JsonArray([.. node.Items.Select(i => i.ToJson())]),
      ConfigNodeKind.Boolean => JsonValue.Create(node.AsBoolean()),
      ConfigNodeKind.Number => JsonValue.Create(node.AsNumber()),
      ConfigNodeKind.Text => JsonValue.Create(node.AsText()),
      var _ => null,
    };

  #endregion
}
