// ------------------------------------------------------------------------------------------------
// <copyright file="SettingsChangeWatcherTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Globalization;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using CodeCoverage.Domain.Ports;
using CodeCoverage.Domain.Settings;
using CodeCoverage.Infrastructure.Identity;
using CodeCoverage.Infrastructure.Persistence;
using CodeCoverage.Infrastructure.Settings;
using CodeCoverage.Infrastructure.Tests.Support;

namespace CodeCoverage.Infrastructure.Tests.Settings;

// Two configuration roots on the same database stand for the two hosts: one saves, the other watches.
[Collection(DatabaseCollection.Name)]
public sealed class SettingsChangeWatcherTests(DatabaseFixture database)
{
  #region Fields

  // Refuses every connection at once: the failure path without Docker.
  private const string UnreachableDatabase = "Host=127.0.0.1;Port=1;Database=codecoverage;Timeout=1";

  private static readonly SettingDefinition AppSlug = SettingDefinitions.Find(SettingDefinitions.GitHubAppSlug)!;

  #endregion

  #region Properties

  private static CancellationToken Ct => TestContext.Current.CancellationToken;

  #endregion

  #region Methods

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task Poll_WhenOtherRootSaved_ShouldNotifyOptionsMonitor(DatabaseProvider provider)
  {
    var connectionString = await MigratedDatabaseAsync(provider);
    var saving = new ConfigurationBuilder().AddCodeCoverageSettingsStore(connectionString, provider).Build();
    var watching = new ConfigurationBuilder().AddCodeCoverageSettingsStore(connectionString, provider).Build();
    await using var services = WatchingHost(watching);
    var watcher = services.GetRequiredService<SettingsChangeWatcher>();
    var monitor = services.GetRequiredService<IOptionsMonitor<GitHubTestOptions>>();
    monitor.CurrentValue.AppSlug.ShouldBeNull();
    string? notified = null;
    using var subscription = monitor.OnChange(o => notified = o.AppSlug);

    await SaveAsync(provider, connectionString, saving, "my-app");

    // The saving host sees its change at once; the watching host only after its poll.
    saving[SettingDefinitions.GitHubAppSlug].ShouldBe("my-app");
    watching[SettingDefinitions.GitHubAppSlug].ShouldBeNull();

    (await watcher.PollAsync(Ct)).ShouldBeTrue();

    notified.ShouldBe("my-app");
    monitor.CurrentValue.AppSlug.ShouldBe("my-app");
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task Poll_WhenRevisionUnchanged_ShouldNotReload(DatabaseProvider provider)
  {
    var connectionString = await MigratedDatabaseAsync(provider);
    var watching = new ConfigurationBuilder().AddCodeCoverageSettingsStore(connectionString, provider).Build();
    await using var services = WatchingHost(watching);
    var watcher = services.GetRequiredService<SettingsChangeWatcher>();
    var reloads = 0;
    using var subscription = services.GetRequiredService<IOptionsMonitor<GitHubTestOptions>>().OnChange(_ => reloads++);

    (await watcher.PollAsync(Ct)).ShouldBeFalse();
    (await watcher.PollAsync(Ct)).ShouldBeFalse();

    reloads.ShouldBe(0);
  }

  // The saving host refreshed itself: its own watcher must not reload it a second time.
  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task Poll_OnTheSavingHost_ShouldNotReloadAgain(DatabaseProvider provider)
  {
    var connectionString = await MigratedDatabaseAsync(provider);
    var saving = new ConfigurationBuilder().AddCodeCoverageSettingsStore(connectionString, provider).Build();
    await using var services = WatchingHost(saving);
    var watcher = services.GetRequiredService<SettingsChangeWatcher>();

    await SaveAsync(provider, connectionString, saving, "my-app");

    (await watcher.PollAsync(Ct)).ShouldBeFalse();
  }

  // The acceptance: running on its timer, the watcher brings the other host's change within the delay.
  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task Execute_WhenOtherRootSaved_ShouldNotifyWithinTheDelay(DatabaseProvider provider)
  {
    var connectionString = await MigratedDatabaseAsync(provider);
    var saving = new ConfigurationBuilder().AddCodeCoverageSettingsStore(connectionString, provider).Build();
    var watching = new ConfigurationBuilder().AddCodeCoverageSettingsStore(connectionString, provider).Build();
    await using var services = WatchingHost(watching);
    var watcher = services.GetRequiredService<SettingsChangeWatcher>();
    var notified = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
    using var subscription = services.GetRequiredService<IOptionsMonitor<GitHubTestOptions>>()
                                     .OnChange(o => notified.TrySetResult(o.AppSlug));

    await watcher.StartAsync(Ct);

    try
    {
      await SaveAsync(provider, connectionString, saving, "my-app");

      (await notified.Task.WaitAsync(TimeSpan.FromSeconds(SettingsWatcherOptions.DefaultPollSeconds), Ct)).ShouldBe("my-app");
    }
    finally
    {
      await watcher.StopAsync(CancellationToken.None);
    }
  }

  // A host that started before the Api migrated catches up once the schema exists.
  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task Poll_WhenStartedBeforeMigration_ShouldLoadOnceMigrated(DatabaseProvider provider)
  {
    var connectionString = await database.NewDatabaseAsync(provider, Ct);
    var watching = new ConfigurationBuilder().AddCodeCoverageSettingsStore(connectionString, provider).Build();
    await using var services = WatchingHost(watching);
    var watcher = services.GetRequiredService<SettingsChangeWatcher>();

    (await watcher.PollAsync(Ct)).ShouldBeFalse();

    await using (var context = DatabaseFixture.CreateContext(provider, connectionString))
    {
      await context.Database.MigrateAsync(Ct);
    }

    (await watcher.PollAsync(Ct)).ShouldBeTrue();
    (await watcher.PollAsync(Ct)).ShouldBeFalse();
  }

  [Fact]
  public async Task Poll_WhenServerUnreachable_ShouldNotThrowNorReload()
  {
    var watching = new ConfigurationBuilder().AddCodeCoverageSettingsStore(UnreachableDatabase, DatabaseProvider.PostgreSql).Build();
    await using var services = WatchingHost(watching);
    var watcher = services.GetRequiredService<SettingsChangeWatcher>();

    (await watcher.PollAsync(Ct)).ShouldBeFalse();
  }

  [Fact]
  public async Task Execute_WithoutSettingsStore_ShouldEndAtOnce()
  {
    var configuration = new ConfigurationBuilder().AddInMemoryCollection().Build();
    await using var services = WatchingHost(configuration);
    var watcher = services.GetRequiredService<SettingsChangeWatcher>();

    await watcher.StartAsync(Ct);

    // Ends on its own, without being stopped.
    var task = watcher.ExecuteTask.ShouldNotBeNull();
    await task.WaitAsync(TimeSpan.FromSeconds(5), Ct);
    task.IsCompletedSuccessfully.ShouldBeTrue();
    (await watcher.PollAsync(Ct)).ShouldBeFalse();
  }

  [Fact]
  public async Task Execute_WhenStopped_ShouldEndWithoutError()
  {
    var watching = new ConfigurationBuilder().AddCodeCoverageSettingsStore(UnreachableDatabase, DatabaseProvider.PostgreSql).Build();
    await using var services = WatchingHost(watching);
    var watcher = services.GetRequiredService<SettingsChangeWatcher>();

    await watcher.StartAsync(Ct);
    await watcher.StopAsync(Ct);

    // Since .NET 10, BackgroundService runs ExecuteAsync through Task.Run on the stopping token: a stop
    // that comes before the thread pool picks it up cancels the task before ExecuteAsync runs. Either
    // way the watcher ends without a fault.
    var task = watcher.ExecuteTask.ShouldNotBeNull();
    await Should.NotThrowAsync(async () =>
    {
      try
      {
        await task;
      }
      catch (OperationCanceledException)
      {
        // Canceled before it ran: the stop came first.
      }
    });
    (task.IsCompletedSuccessfully || task.IsCanceled).ShouldBeTrue();
  }

  [Fact]
  public void Ctor_WithNullArgument_ShouldThrowArgumentNullException()
  {
    var configuration = new ConfigurationBuilder().Build();
    var options = A.Fake<IOptionsMonitor<SettingsWatcherOptions>>();
    var logger = NullLogger<SettingsChangeWatcher>.Instance;

    Should.Throw<ArgumentNullException>(() => new SettingsChangeWatcher(null!, options, TimeProvider.System, logger));
    Should.Throw<ArgumentNullException>(() => new SettingsChangeWatcher(configuration, null!, TimeProvider.System, logger));
    Should.Throw<ArgumentNullException>(() => new SettingsChangeWatcher(configuration, options, null!, logger));
    Should.Throw<ArgumentNullException>(() => new SettingsChangeWatcher(configuration, options, TimeProvider.System, null!));
  }

  [Theory]
  [InlineData(null, true)]
  [InlineData("1", true)]
  [InlineData("300", true)]
  [InlineData("0", false)]
  [InlineData("301", false)]
  public async Task Options_ShouldBeValidatedWithinBounds(string? pollSeconds, bool isValid)
  {
    // Without the key at all for the default case.
    Dictionary<string, string?> settings = pollSeconds is null ? [] : new() { ["Settings:PollSeconds"] = pollSeconds };
    var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
    await using var services = new ServiceCollection()
                               .AddSingleton<IConfiguration>(configuration)
                               .AddLogging()
                               .AddInfrastructure()
                               .BuildServiceProvider();

    var read = () => services.GetRequiredService<IOptions<SettingsWatcherOptions>>().Value;

    if (isValid)
    {
      read().PollSeconds.ShouldBe(pollSeconds is null ? SettingsWatcherOptions.DefaultPollSeconds : int.Parse(pollSeconds, CultureInfo.InvariantCulture));
    }
    else
    {
      Should.Throw<OptionsValidationException>(read).Message.ShouldContain("Settings:PollSeconds");
    }
  }

  private static async Task SaveAsync(DatabaseProvider provider, string connectionString, IConfiguration configuration, string value)
  {
    await using var context = DatabaseFixture.CreateContext(provider, connectionString);
    var user = new CurrentUserContext();
    user.SetUser(7, "octocat", isAdmin: true);
    var clock = A.Fake<IClock>();
    A.CallTo(() => clock.UtcNow).Returns(new DateTimeOffset(2026, 11, 8, 10, 30, 0, TimeSpan.Zero));

    await new ConfigurationSettingsStore(context, configuration, user, clock).SaveAsync(AppSlug, value, Ct);
  }

  // The watching host: its configuration, an options class read through IOptionsMonitor, and the watcher.
  private static ServiceProvider WatchingHost(IConfigurationRoot configuration)
  {
    var services = new ServiceCollection()
                   .AddSingleton<IConfiguration>(configuration)
                   .AddSingleton(TimeProvider.System)
                   .AddLogging()
                   .AddSingleton<SettingsChangeWatcher>();

    services.AddOptions<SettingsWatcherOptions>().Configure(static o => o.PollSeconds = SettingsWatcherOptions.MinPollSeconds);
    services.AddOptions<GitHubTestOptions>().Bind(configuration.GetSection(SettingDefinitions.GitHubGroup));

    return services.BuildServiceProvider();
  }

  private async Task<string> MigratedDatabaseAsync(DatabaseProvider provider)
  {
    var connectionString = await database.NewDatabaseAsync(provider, Ct);

    await using var context = DatabaseFixture.CreateContext(provider, connectionString);
    await context.Database.MigrateAsync(Ct);

    return connectionString;
  }

  #endregion
}
