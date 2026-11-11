// ------------------------------------------------------------------------------------------------
// <copyright file="DependencyInjection.Commits.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.Extensions.DependencyInjection;

using CodeCoverage.Application.Services;

namespace CodeCoverage.Application;

/// <content>Registers the services of the commits area.</content>
public static partial class DependencyInjection
{
  #region Methods

  /// <summary>Registers the services of the commits area, each with its explicit lifetime.</summary>
  /// <param name="services">The service collection.</param>
  /// <returns>The same collection, for chaining.</returns>
  private static IServiceCollection AddCommitServices(this IServiceCollection services)
  {
    services.AddScoped<CommitAncestry>();
    services.AddScoped<BranchHeadTracker>();
    services.AddScoped<PushProcessor>();
    services.AddScoped<ConfigResolver>();
    services.AddScoped<RepositoryTreeProvider>();
    services.AddScoped<DiffProvider>();
    services.AddScoped<MergeCommitDetector>();
    services.AddSingleton<CommitMerger>();
    services.AddScoped<CarryforwardResolver>();

    return services;
  }

  #endregion
}
