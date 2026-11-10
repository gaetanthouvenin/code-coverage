// ------------------------------------------------------------------------------------------------
// <copyright file="DependencyInjection.Repositories.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.Extensions.DependencyInjection;

using CodeCoverage.Domain.Ports;
using CodeCoverage.Infrastructure.Persistence.Repositories;

namespace CodeCoverage.Infrastructure;

public static partial class DependencyInjection
{
  #region Methods

  /// <summary>Registers the repositories of <c>Persistence/Repositories</c>, the database adapters of the repository ports.</summary>
  /// <param name="services">The service collection.</param>
  /// <returns>The same collection, for chaining.</returns>
  private static IServiceCollection AddRepositories(this IServiceCollection services)
  {
    services.AddScoped<IOrganizationDirectory, OrganizationDirectory>();
    services.AddScoped<IRepositoryRepository, RepositoryRepository>();
    services.AddScoped<IInstallationRepository, InstallationRepository>();
    services.AddScoped<IBranchRepository, BranchRepository>();
    services.AddScoped<ICommitRepository, CommitRepository>();
    services.AddScoped<ICommitConfigRepository, CommitConfigRepository>();
    services.AddScoped<ICommitReportRepository, CommitReportRepository>();
    services.AddScoped<IComparisonRepository, ComparisonRepository>();
    services.AddScoped<IDiffCacheRepository, DiffCacheRepository>();
    services.AddScoped<IGitCommitRepository, GitCommitRepository>();
    services.AddScoped<IRepositoryTreeRepository, RepositoryTreeRepository>();
    services.AddScoped<IPullRequestRepository, PullRequestRepository>();
    services.AddScoped<IUploadRepository, UploadRepository>();
    services.AddScoped<IUploadTokenRepository, UploadTokenRepository>();
    services.AddScoped<IUserRepository, UserRepository>();
    services.AddScoped<IUserRepositoryAccessRepository, UserRepositoryAccessRepository>();
    services.AddScoped<IWebhookDeliveryRepository, WebhookDeliveryRepository>();

    return services;
  }

  #endregion
}
