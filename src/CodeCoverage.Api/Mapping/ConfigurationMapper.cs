// ------------------------------------------------------------------------------------------------
// <copyright file="ConfigurationMapper.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using CodeCoverage.Api.Contracts.Configuration;
using CodeCoverage.Application.Features.Configuration.Queries.ValidateCoverageYaml;
using CodeCoverage.Domain.Config;

namespace CodeCoverage.Api.Mapping;

/// <summary>Converts the coverage configuration between the Domain and the API.</summary>
internal static class ConfigurationMapper
{
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
        ? ConfigNodeJson.ToJsonElement(normalized)
        : null,
      [.. result.Warnings.Select(w => new ConfigWarningModel(w.Code, w.Key, w.Detail))],
      result.Errors);

  #endregion
}
