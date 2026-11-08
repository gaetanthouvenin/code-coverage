// ------------------------------------------------------------------------------------------------
// <copyright file="DependencyInjection.Uploads.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.Extensions.DependencyInjection;

namespace CodeCoverage.Infrastructure;

public static partial class DependencyInjection
{
  #region Methods

  /// <summary>Registers the adapters of the uploads area: the storage of the uploaded reports and the upload tokens.</summary>
  /// <param name="services">The service collection.</param>
  /// <returns>The same collection, for chaining.</returns>
  private static IServiceCollection AddUploads(this IServiceCollection services)
  {
    return services;
  }

  #endregion
}
