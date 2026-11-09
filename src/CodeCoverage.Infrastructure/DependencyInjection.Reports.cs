// ------------------------------------------------------------------------------------------------
// <copyright file="DependencyInjection.Reports.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using CodeCoverage.Domain.Ports;
using CodeCoverage.Infrastructure.Reports;
using CodeCoverage.Infrastructure.Reports.Codecs;
using CodeCoverage.Infrastructure.Reports.Parsers;
using CodeCoverage.Infrastructure.Yaml;

namespace CodeCoverage.Infrastructure;

public static partial class DependencyInjection
{
  #region Methods

  /// <summary>
  ///   Registers the adapters of the reports area: the parsers of the coverage reports and of their
  ///   configuration file, and their caches.
  /// </summary>
  /// <param name="services">The service collection.</param>
  /// <returns>The same collection, for chaining.</returns>
  private static IServiceCollection AddReports(this IServiceCollection services)
  {
    services.TryAddSingleton<ICoverageConfigParser, YamlCoverageConfigParser>();
    services.TryAddEnumerable(ServiceDescriptor.Singleton<IReportParser, LcovParser>());
    services.TryAddEnumerable(ServiceDescriptor.Singleton<IReportParser, CoberturaParser>());
    services.TryAddEnumerable(ServiceDescriptor.Singleton<IReportParser, OpenCoverParser>());
    services.TryAddSingleton<IReportParserCatalog, ReportParserCatalog>();
    services.TryAddSingleton<ICoverageReportCodec, CcrCodec>();
    services.TryAddEnumerable(ServiceDescriptor.Singleton<IReportParser, CloverParser>());

    return services;
  }

  #endregion
}
