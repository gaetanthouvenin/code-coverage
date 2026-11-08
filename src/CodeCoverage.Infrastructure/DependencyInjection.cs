// ------------------------------------------------------------------------------------------------
// <copyright file="DependencyInjection.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

using CodeCoverage.Domain.Ports;
using CodeCoverage.Infrastructure.Diagnostics;
using CodeCoverage.Infrastructure.HealthChecks;
using CodeCoverage.Infrastructure.Identity;
using CodeCoverage.Infrastructure.Jobs;
using CodeCoverage.Infrastructure.Operations;
using CodeCoverage.Infrastructure.Persistence;
using CodeCoverage.Infrastructure.Security;
using CodeCoverage.Infrastructure.Settings;
using CodeCoverage.Infrastructure.Time;

namespace CodeCoverage.Infrastructure;

/// <summary>Registers the adapters of the Domain ports.</summary>
public static class DependencyInjection
{
  #region Fields

  /// <summary>The tag of the health checks that make up the readiness probe (<c>/health/ready</c>).</summary>
  public const string ReadyTag = "ready";

  /// <summary>The name of the database connectivity check in the health report.</summary>
  public const string DatabaseHealthCheckName = "database";

  #endregion

  #region Methods

  /// <summary>
  ///   Registers the migrator that applies the pending migrations when the host starts, unless
  ///   <c>CodeCoverage:Database:MigrateOnStartup</c> is <see langword="false" />.
  /// </summary>
  /// <param name="services">The service collection.</param>
  /// <returns>The same collection, for chaining.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="services" /> is <see langword="null" />.</exception>
  /// <remarks>Called by the Api only: the Web never migrates, it waits for the Api (readiness probe).</remarks>
  public static IServiceCollection AddDatabaseMigration(this IServiceCollection services)
  {
    ArgumentNullException.ThrowIfNull(services);

    services.AddHostedService<DatabaseMigrator>();

    return services;
  }

  /// <summary>
  ///   Registers the job worker, which runs the background jobs, the sweeper of the expired leases, the
  ///   scheduler of the recurring jobs and the sampler of the job queue metrics.
  /// </summary>
  /// <param name="services">The service collection.</param>
  /// <returns>The same collection, for chaining.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="services" /> is <see langword="null" />.</exception>
  /// <remarks>
  ///   <para>
  ///     Called by the Api only, next to <see cref="AddInfrastructure" />: the Web queues jobs but never runs
  ///     them. The worker, the sweeper and the scheduler are idle when <c>Worker:Enabled</c> is
  ///     <see langword="false" /> (<see cref="WorkerOptions" />), so that a replica of the Api may serve requests
  ///     only; the <see cref="JobQueueMetricsSampler" /> measures the shared queue in any case.
  ///   </para>
  ///   <para>
  ///     <see cref="JobDispatcher" /> is scoped: it is resolved in the scope of each job, with the mediator of
  ///     that scope.
  ///   </para>
  ///   <para>
  ///     Each definition of the <see cref="RecurringJobCatalog" /> is registered as a singleton, read by the
  ///     <see cref="RecurringJobScheduler" />.
  ///   </para>
  /// </remarks>
  public static IServiceCollection AddJobProcessing(this IServiceCollection services)
  {
    ArgumentNullException.ThrowIfNull(services);

    services.AddOptions<WorkerOptions>().BindConfiguration(WorkerOptions.SectionName);
    services.TryAddSingleton(TimeProvider.System);
    services.TryAddSingleton<JobWakeUpSignal>();
    services.TryAddSingleton<JobHeartbeat>();
    services.TryAddScoped<IJobDispatcher, JobDispatcher>();
    services.AddHostedService<JobWorker>();
    services.AddHostedService<LeaseSweeper>();

    foreach (var definition in RecurringJobCatalog.All)
    {
      services.AddSingleton(definition);
    }

    services.AddHostedService<RecurringJobScheduler>();
    services.AddHostedService<JobQueueMetricsSampler>();

    return services;
  }

  /// <summary>
  ///   Registers the current principal of each scope, the clock, the token hasher, the metrics, the
  ///   database, its health checks, the audit trail, the job queue, the settings store and the watcher that
  ///   brings in the settings saved by the other host.
  /// </summary>
  /// <param name="services">The service collection.</param>
  /// <returns>The same collection, for chaining.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="services" /> is <see langword="null" />.</exception>
  /// <remarks>
  ///   <para>
  ///     <see cref="CurrentUserContext" /> is scoped and resolved both as itself (the hosts set the
  ///     user on it) and as <see cref="ICurrentUser" /> (the use cases read it): one instance per
  ///     scope.
  ///   </para>
  ///   <para>
  ///     <see cref="CodeCoverageMeter" /> is a singleton whose meter comes from the
  ///     <see cref="System.Diagnostics.Metrics.IMeterFactory" /> of the host (registered here too when the host
  ///     has none); the hosts export it with OpenTelemetry when an OTLP endpoint is configured.
  ///   </para>
  ///   <para>
  ///     <see cref="CodeCoverageDbContext" /> reads the engine from <see cref="DatabaseOptions" /> and
  ///     the connection string <c>ConnectionStrings:CodeCoverage</c> when a context is first created,
  ///     not at registration: a host that never touches the database needs neither.
  ///   </para>
  ///   <para>
  ///     <see cref="SqlJobQueue" /> is scoped and resolved both as itself (the repositories queue their
  ///     jobs in their own transaction through it) and as <see cref="IJobQueue" />: one instance per
  ///     scope. It claims the jobs with the <see cref="IJobClaimDialect" /> of the configured engine,
  ///     registered by the migrations assemblies (<c>AddPostgreSqlProvider()</c>,
  ///     <c>AddSqlServerProvider()</c>), which the hosts call next to this method. Once a job is committed,
  ///     it notifies the <see cref="JobWakeUpSignal" /> of the process; the jobs themselves only run where
  ///     <see cref="AddJobProcessing" /> is called (the Api).
  ///   </para>
  ///   <para>
  ///     The secret protector needs the key ring of the host: it is registered by
  ///     <see cref="DataProtectionConfiguration.AddCodeCoverageDataProtection" />.
  ///   </para>
  ///   <para>
  ///     <see cref="SettingsChangeWatcher" /> polls the settings revision every <c>Settings:PollSeconds</c>
  ///     (5 s by default) in both hosts, so that a setting saved on one host reaches the other one.
  ///   </para>
  ///   <para>
  ///     The readiness checks (<see cref="ReadyTag" />): the database answers, every migration is
  ///     applied, and <c>READ_COMMITTED_SNAPSHOT</c> is on under SQL Server.
  ///   </para>
  /// </remarks>
  public static IServiceCollection AddInfrastructure(this IServiceCollection services)
  {
    ArgumentNullException.ThrowIfNull(services);

    services.AddScoped<CurrentUserContext>();
    services.AddScoped<ICurrentUser>(static sp => sp.GetRequiredService<CurrentUserContext>());

    services.TryAddSingleton(TimeProvider.System);
    services.TryAddSingleton<IClock, SystemClock>();
    services.TryAddSingleton<ITokenHasher, Sha256TokenHasher>();

    services.AddMetrics();
    services.TryAddSingleton<CodeCoverageMeter>();

    services.AddOptions<DatabaseOptions>().BindConfiguration(DatabaseOptions.SectionName);
    services.AddDbContext<CodeCoverageDbContext>(static (sp, options) => ConfigureDatabase(sp, options));
    services.AddScoped<IAuditLog, AuditLog>();
    services.TryAddSingleton<IJobClaimDialect>(static sp => ResolveJobClaimDialect(sp));
    services.TryAddSingleton<JobWakeUpSignal>();
    services.AddScoped<SqlJobQueue>();
    services.AddScoped<IJobQueue>(static sp => sp.GetRequiredService<SqlJobQueue>());
    services.AddScoped<ISettingsStore, ConfigurationSettingsStore>();

    services.AddOptions<SettingsWatcherOptions>()
            .BindConfiguration(SettingsWatcherOptions.SectionName)
            .Validate(
              static o => o.IsValid(),
              $"{SettingsWatcherOptions.SectionName}:{nameof(SettingsWatcherOptions.PollSeconds)} must be between "
              + $"{SettingsWatcherOptions.MinPollSeconds} and {SettingsWatcherOptions.MaxPollSeconds}."
            )
            .ValidateOnStart();
    services.AddHostedService<SettingsChangeWatcher>();

    services.AddHealthChecks()
            .AddDbContextCheck<CodeCoverageDbContext>(DatabaseHealthCheckName, tags: [ReadyTag])
            .AddCheck<MigrationsAppliedHealthCheck>(MigrationsAppliedHealthCheck.Name, tags: [ReadyTag])
            .AddCheck<ReadCommittedSnapshotHealthCheck>(ReadCommittedSnapshotHealthCheck.Name, tags: [ReadyTag]);

    return services;
  }

  /// <summary>Points the context options at the configured engine and connection string.</summary>
  /// <param name="services">The services of the scope.</param>
  /// <param name="options">The context options.</param>
  /// <exception cref="InvalidOperationException">The connection string is missing.</exception>
  private static void ConfigureDatabase(IServiceProvider services, DbContextOptionsBuilder options)
  {
    var database = services.GetRequiredService<IOptions<DatabaseOptions>>().Value;
    var connectionString = services.GetRequiredService<IConfiguration>()
                                   .GetConnectionString(DatabaseOptions.ConnectionStringName);

    if (string.IsNullOrWhiteSpace(connectionString))
    {
      throw new InvalidOperationException(
        $"The connection string 'ConnectionStrings:{DatabaseOptions.ConnectionStringName}' is missing."
      );
    }

    options.UseCodeCoverageDatabase(database.Provider, connectionString);
  }

  /// <summary>Picks the job claim dialect of the configured engine.</summary>
  /// <param name="services">The root services.</param>
  /// <returns>The dialect registered for <see cref="DatabaseOptions.Provider" />.</returns>
  /// <exception cref="InvalidOperationException">No dialect is registered for the configured engine.</exception>
  private static IJobClaimDialect ResolveJobClaimDialect(IServiceProvider services)
  {
    var provider = services.GetRequiredService<IOptions<DatabaseOptions>>().Value.Provider;

    return services.GetKeyedService<IJobClaimDialect>(provider)
           ?? throw new InvalidOperationException(
             $"No job claim dialect is registered for the database provider '{provider}': call "
             + "AddPostgreSqlProvider() and AddSqlServerProvider() next to AddInfrastructure()."
           );
  }

  #endregion
}
