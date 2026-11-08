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
  #region Methods

  /// <summary>Registers the controllers, with the enumerations serialized as their names.</summary>
  /// <param name="services">The service collection.</param>
  /// <returns>The same collection, for chaining.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="services" /> is <see langword="null" />.</exception>
  public static IServiceCollection AddPresentation(this IServiceCollection services)
  {
    ArgumentNullException.ThrowIfNull(services);

    services.AddControllers()
            .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

    return services;
  }

  #endregion
}
