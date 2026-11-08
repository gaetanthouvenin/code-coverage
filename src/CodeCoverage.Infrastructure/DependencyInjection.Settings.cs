// ------------------------------------------------------------------------------------------------
// <copyright file="DependencyInjection.Settings.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.Extensions.DependencyInjection;

using CodeCoverage.Application.Configuration;

namespace CodeCoverage.Infrastructure;

public static partial class DependencyInjection
{
  #region Methods

  /// <summary>Binds the options of the settings groups to their configuration sections.</summary>
  /// <param name="services">The service collection.</param>
  /// <returns>The same collection, for chaining.</returns>
  /// <remarks>
  ///   The options follow the configuration, the settings store included: a consumer that reads them through
  ///   <c>IOptionsMonitor&lt;T&gt;</c> sees a change without a restart.
  /// </remarks>
  private static IServiceCollection AddSettings(this IServiceCollection services)
  {
    services.AddOptions<CoverageOptions>().BindConfiguration(CoverageOptions.SectionName).ValidateOnStart();
    services.AddOptions<UploadOptions>().BindConfiguration(UploadOptions.SectionName).ValidateOnStart();
    services.AddOptions<ProcessingOptions>().BindConfiguration(ProcessingOptions.SectionName).ValidateOnStart();
    services.AddOptions<NotificationOptions>().BindConfiguration(NotificationOptions.SectionName).ValidateOnStart();
    services.AddOptions<AccessOptions>().BindConfiguration(AccessOptions.SectionName).ValidateOnStart();
    services.AddOptions<BadgeOptions>().BindConfiguration(BadgeOptions.SectionName).ValidateOnStart();
    services.AddOptions<RetentionOptions>().BindConfiguration(RetentionOptions.SectionName).ValidateOnStart();

    return services;
  }

  #endregion
}
