// ------------------------------------------------------------------------------------------------
// <copyright file="PresentationConfiguration.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Text.Json.Serialization;

namespace CodeCoverage.Api.Configuration;

/// <summary>Registers the MVC controllers of the REST API.</summary>
public static class PresentationConfiguration
{
  #region Fields

  /// <summary>The depth of the JSON the API reads and writes.</summary>
  /// <remarks>
  ///   Above the 32 levels of MVC: the validation returns a <c>codecov.yml</c> file as read, and the YAML
  ///   parser accepts 64 levels.
  /// </remarks>
  internal const int MaximumJsonDepth = 128;

  #endregion

  #region Methods

  /// <summary>Registers the controllers, with the enumerations serialized as their names.</summary>
  /// <param name="services">The service collection.</param>
  /// <returns>The same collection, for chaining.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="services" /> is <see langword="null" />.</exception>
  public static IServiceCollection AddPresentation(this IServiceCollection services)
  {
    ArgumentNullException.ThrowIfNull(services);

    services.AddControllers()
            .AddJsonOptions(o =>
                            {
                              o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
                              o.JsonSerializerOptions.MaxDepth = MaximumJsonDepth;
                            });

    return services;
  }

  #endregion
}
