// ------------------------------------------------------------------------------------------------
// <copyright file="SettingsOptionsBindingTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using CodeCoverage.Application.Configuration;
using CodeCoverage.Domain.Settings;
using CodeCoverage.Infrastructure.GitHub;

namespace CodeCoverage.Infrastructure.Tests.Settings;

public sealed class SettingsOptionsBindingTests
{
  #region Methods

  [Fact]
  public async Task AddInfrastructure_WhenTheConfigurationChanges_ShouldReflectTheNewValues()
  {
    var configuration = new ConfigurationBuilder().AddInMemoryCollection().Build();
    await using var services = new ServiceCollection()
                               .AddSingleton<IConfiguration>(configuration)
                               .AddLogging()
                               .AddInfrastructure()
                               .BuildServiceProvider();

    ShouldFollow<CoverageOptions>(services, configuration, SettingDefinitions.CoverageAllowFilenameOnlyMatch, "true", static o => o.AllowFilenameOnlyMatch, true);
    ShouldFollow<UploadOptions>(services, configuration, SettingDefinitions.UploadsMaxFilesPerUpload, "7", static o => o.MaxFilesPerUpload, 7);
    ShouldFollow<ProcessingOptions>(services, configuration, SettingDefinitions.ProcessingJobMaxAttempts, "3", static o => o.JobMaxAttempts, 3);
    ShouldFollow<NotificationOptions>(services, configuration, SettingDefinitions.NotificationsCommentTitle, "Report", static o => o.CommentTitle, "Report");
    ShouldFollow<AccessOptions>(services, configuration, SettingDefinitions.AccessCacheMinutes, "25", static o => o.CacheMinutes, 25);
    ShouldFollow<BadgeOptions>(services, configuration, SettingDefinitions.BadgesLabel, "tests", static o => o.Label, "tests");
    ShouldFollow<RetentionOptions>(services, configuration, SettingDefinitions.RetentionRawDays, "45", static o => o.RawDays, 45);
    ShouldFollow<GitHubOptions>(services, configuration, SettingDefinitions.GitHubClientId, "Iv1.new", static o => o.ClientId, "Iv1.new");
  }

  private static void ShouldFollow<TOptions>(
    ServiceProvider services,
    IConfigurationRoot configuration,
    string key,
    string value,
    Func<TOptions, object?> read,
    object expected)
  {
    var monitor = services.GetRequiredService<IOptionsMonitor<TOptions>>();

    read(monitor.CurrentValue).ShouldNotBe(expected, key);

    configuration[key] = value;
    configuration.Reload();

    read(monitor.CurrentValue).ShouldBe(expected, key);
  }

  #endregion
}
