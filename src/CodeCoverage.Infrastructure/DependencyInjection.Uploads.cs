// ------------------------------------------------------------------------------------------------
// <copyright file="DependencyInjection.Uploads.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using CodeCoverage.Application.Configuration;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Infrastructure.Caching;
using CodeCoverage.Infrastructure.Identity;
using CodeCoverage.Infrastructure.Security;
using CodeCoverage.Infrastructure.Uploads;

namespace CodeCoverage.Infrastructure;

public static partial class DependencyInjection
{
  #region Methods

  /// <summary>Registers the adapters of the uploads area: the storage of the uploaded reports and the upload tokens.</summary>
  /// <param name="services">The service collection.</param>
  /// <returns>The same collection, for chaining.</returns>
  /// <remarks>The OIDC token validator is a singleton: it owns the cache of the signing keys of GitHub.</remarks>
  private static IServiceCollection AddUploads(this IServiceCollection services)
  {
    services.AddHttpClient(GitHubOidcTokenValidator.HttpClientName, static client => client.Timeout = GitHubOidcTokenValidator.MetadataTimeout);
    services.AddSingleton<IOidcTokenValidator>(
      static provider => new GitHubOidcTokenValidator(
        GitHubOidcTokenValidator.CreateConfigurationManager(
          provider.GetRequiredService<IHttpClientFactory>().CreateClient(GitHubOidcTokenValidator.HttpClientName)
        ),
        provider.GetRequiredService<IOptionsMonitor<UploadOptions>>(),
        provider.GetRequiredService<IConfiguration>(),
        provider.GetRequiredService<ILogger<GitHubOidcTokenValidator>>()
      )
    );

    services.AddScoped<IRawUploadStore, SqlRawUploadStore>();
    services.AddScoped<IUploadCredentialCache, MemoryUploadCredentialCache>();
    services.AddScoped<IReportViewCache, MemoryReportViewCache>();
    services.AddSingleton<SourceContentMemory>();
    services.AddScoped<ISourceContentCache, BoundedSourceContentCache>();
    services.AddSingleton<ITokenGenerator, TokenGenerator>();

    return services;
  }

  #endregion
}
