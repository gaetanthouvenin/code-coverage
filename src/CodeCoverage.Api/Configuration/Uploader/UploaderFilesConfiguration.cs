// ------------------------------------------------------------------------------------------------
// <copyright file="UploaderFilesConfiguration.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Api.Configuration.Uploader;

/// <summary>Registers the options of the standalone binaries of the uploader.</summary>
public static class UploaderFilesConfiguration
{
  #region Methods

  /// <summary>Binds <see cref="UploaderFilesOptions" /> to its bootstrap section.</summary>
  /// <param name="services">The service collection.</param>
  /// <param name="configuration">The application configuration.</param>
  /// <returns>The same collection, for chaining.</returns>
  /// <exception cref="ArgumentNullException">
  ///   <paramref name="services" /> or <paramref name="configuration" /> is <see langword="null" />.
  /// </exception>
  public static IServiceCollection AddUploaderFiles(this IServiceCollection services, IConfiguration configuration)
  {
    ArgumentNullException.ThrowIfNull(services);
    ArgumentNullException.ThrowIfNull(configuration);

    services.Configure<UploaderFilesOptions>(configuration.GetSection(UploaderFilesOptions.SectionName));

    return services;
  }

  #endregion
}
