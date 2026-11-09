// ------------------------------------------------------------------------------------------------
// <copyright file="DependencyInjectionTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

using CodeCoverage.Application;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Infrastructure.Diagnostics;
using CodeCoverage.Infrastructure.GitHub;
using CodeCoverage.Infrastructure.Identity;
using CodeCoverage.Infrastructure.Jobs;
using CodeCoverage.Infrastructure.Operations;
using CodeCoverage.Infrastructure.Persistence;
using CodeCoverage.Infrastructure.Persistence.Repositories;
using CodeCoverage.Infrastructure.PostgreSql;
using CodeCoverage.Infrastructure.PostgreSql.Jobs;
using CodeCoverage.Infrastructure.Reports;
using CodeCoverage.Infrastructure.Reports.Codecs;
using CodeCoverage.Infrastructure.Security;
using CodeCoverage.Infrastructure.Settings;
using CodeCoverage.Infrastructure.SqlServer;
using CodeCoverage.Infrastructure.SqlServer.Jobs;
using CodeCoverage.Infrastructure.Time;
using CodeCoverage.Infrastructure.Yaml;

namespace CodeCoverage.Infrastructure.Tests;

public sealed class DependencyInjectionTests
{
  #region Methods

  [Theory]
  [InlineData(null, "Npgsql.EntityFrameworkCore.PostgreSQL")]
  [InlineData("PostgreSql", "Npgsql.EntityFrameworkCore.PostgreSQL")]
  [InlineData("SqlServer", "Microsoft.EntityFrameworkCore.SqlServer")]
  public async Task AddInfrastructure_WithConnectionString_ShouldCreateContextForConfiguredProvider(
    string? provider,
    string providerName)
  {
    await using var services = Build(new Dictionary<string, string?>
    {
      ["ConnectionStrings:CodeCoverage"] = "Server=localhost;Database=unused",
      ["CodeCoverage:Database:Provider"] = provider,
    });
    await using var scope = services.CreateAsyncScope();

    var context = scope.ServiceProvider.GetRequiredService<CodeCoverageDbContext>();

    context.Database.ProviderName.ShouldBe(providerName);
  }

  [Fact]
  public async Task AddInfrastructure_WithoutConnectionString_ShouldThrowInvalidOperationException()
  {
    await using var services = Build(new Dictionary<string, string?>());
    await using var scope = services.CreateAsyncScope();

    Should.Throw<InvalidOperationException>(() => scope.ServiceProvider.GetRequiredService<CodeCoverageDbContext>().Model)
          .Message.ShouldContain("ConnectionStrings:CodeCoverage");
  }

  [Fact]
  public async Task AddInfrastructure_ShouldRegisterTheSystemClock()
  {
    await using var services = Build(new Dictionary<string, string?>());

    services.GetRequiredService<IClock>().ShouldBeOfType<SystemClock>();
  }

  [Fact]
  public async Task AddInfrastructure_ShouldRegisterTheOidcTokenValidatorAsSingleton()
  {
    await using var services = Build(new Dictionary<string, string?>());

    var validator = services.GetRequiredService<IOidcTokenValidator>();

    validator.ShouldBeOfType<GitHubOidcTokenValidator>();
    services.GetRequiredService<IOidcTokenValidator>().ShouldBeSameAs(validator);
  }

  [Fact]
  public async Task AddInfrastructure_ShouldRegisterTheTokenHasher()
  {
    await using var services = Build(new Dictionary<string, string?>());

    services.GetRequiredService<ITokenHasher>().ShouldBeOfType<Sha256TokenHasher>();
  }

  [Fact]
  public async Task AddInfrastructure_ShouldRegisterTheCoverageConfigParser()
  {
    await using var services = Build(new Dictionary<string, string?>());

    services.GetRequiredService<ICoverageConfigParser>().ShouldBeOfType<YamlCoverageConfigParser>();
  }

  [Fact]
  public async Task AddInfrastructure_ShouldRegisterTheReportParserCatalog()
  {
    await using var services = Build(new Dictionary<string, string?>());

    services.GetRequiredService<IReportParserCatalog>().ShouldBeOfType<ReportParserCatalog>();
  }

  [Fact]
  public async Task AddInfrastructure_ShouldRegisterTheCoverageReportCodec()
  {
    await using var services = Build(new Dictionary<string, string?>());

    services.GetRequiredService<ICoverageReportCodec>().ShouldBeOfType<CcrCodec>();
  }

  [Fact]
  public async Task AddInfrastructure_ShouldRegisterTheAuditLogPerScope()
  {
    await using var services = Build(new Dictionary<string, string?>
    {
      ["ConnectionStrings:CodeCoverage"] = "Server=localhost;Database=unused",
    });
    await using var first = services.CreateAsyncScope();
    await using var second = services.CreateAsyncScope();

    var log = first.ServiceProvider.GetRequiredService<IAuditLog>();

    log.ShouldBeOfType<AuditLog>();
    first.ServiceProvider.GetRequiredService<IAuditLog>().ShouldBeSameAs(log);
    second.ServiceProvider.GetRequiredService<IAuditLog>().ShouldNotBeSameAs(log);
  }

  [Fact]
  public async Task AddInfrastructure_ShouldRegisterTheOrganizationDirectoryPerScope()
  {
    await using var services = Build(new Dictionary<string, string?>
    {
      ["ConnectionStrings:CodeCoverage"] = "Server=localhost;Database=unused",
    });
    await using var first = services.CreateAsyncScope();
    await using var second = services.CreateAsyncScope();

    var directory = first.ServiceProvider.GetRequiredService<IOrganizationDirectory>();

    directory.ShouldBeOfType<OrganizationDirectory>();
    second.ServiceProvider.GetRequiredService<IOrganizationDirectory>().ShouldNotBeSameAs(directory);
  }

  [Fact]
  public async Task AddInfrastructure_ShouldRegisterOneOrganizationContextPerScope()
  {
    await using var services = Build(new Dictionary<string, string?>());
    await using var first = services.CreateAsyncScope();
    await using var second = services.CreateAsyncScope();

    var context = first.ServiceProvider.GetRequiredService<OrganizationContext>();

    first.ServiceProvider.GetRequiredService<ICurrentOrganization>().ShouldBeSameAs(context);
    second.ServiceProvider.GetRequiredService<ICurrentOrganization>().ShouldNotBeSameAs(context);
  }

  [Fact]
  public async Task AddInfrastructure_ShouldRegisterTheOrganizationScope()
  {
    await using var services = Build(new Dictionary<string, string?>
    {
      ["ConnectionStrings:CodeCoverage"] = "Server=localhost;Database=unused",
    });
    await using var scope = services.CreateAsyncScope();

    scope.ServiceProvider.GetRequiredService<IOrganizationScope>().ShouldBeOfType<OrganizationScope>();
  }

  [Fact]
  public async Task AddInfrastructure_ShouldRegisterTheSettingsStorePerScope()
  {
    await using var services = Build(new Dictionary<string, string?>
    {
      ["ConnectionStrings:CodeCoverage"] = "Server=localhost;Database=unused",
    });
    await using var first = services.CreateAsyncScope();
    await using var second = services.CreateAsyncScope();

    var store = first.ServiceProvider.GetRequiredService<ISettingsStore>();

    store.ShouldBeOfType<ConfigurationSettingsStore>();
    first.ServiceProvider.GetRequiredService<ISettingsStore>().ShouldBeSameAs(store);
    second.ServiceProvider.GetRequiredService<ISettingsStore>().ShouldNotBeSameAs(store);
  }

  [Fact]
  public async Task AddInfrastructure_ShouldRegisterTheJobQueuePerScope()
  {
    await using var services = Build(
      new Dictionary<string, string?> { ["ConnectionStrings:CodeCoverage"] = "Server=localhost;Database=unused" },
      withProviders: true
    );
    await using var first = services.CreateAsyncScope();
    await using var second = services.CreateAsyncScope();

    var queue = first.ServiceProvider.GetRequiredService<IJobQueue>();

    queue.ShouldBeOfType<SqlJobQueue>();
    first.ServiceProvider.GetRequiredService<SqlJobQueue>().ShouldBeSameAs(queue);
    second.ServiceProvider.GetRequiredService<IJobQueue>().ShouldNotBeSameAs(queue);
  }

  [Theory]
  [InlineData(null, typeof(PostgreSqlJobClaimDialect))]
  [InlineData("PostgreSql", typeof(PostgreSqlJobClaimDialect))]
  [InlineData("SqlServer", typeof(SqlServerJobClaimDialect))]
  public async Task AddProviders_ShouldResolveTheJobClaimDialectOfTheConfiguredProvider(string? provider, Type dialect)
  {
    await using var services = Build(
      new Dictionary<string, string?> { ["CodeCoverage:Database:Provider"] = provider },
      withProviders: true
    );

    services.GetRequiredService<IJobClaimDialect>().ShouldBeOfType(dialect);
    services.GetRequiredKeyedService<IJobClaimDialect>(DatabaseProvider.PostgreSql).ShouldBeOfType<PostgreSqlJobClaimDialect>();
    services.GetRequiredKeyedService<IJobClaimDialect>(DatabaseProvider.SqlServer).ShouldBeOfType<SqlServerJobClaimDialect>();
  }

  [Fact]
  public async Task AddInfrastructure_WithoutProviderAssembly_ShouldThrowOnTheJobClaimDialect()
  {
    await using var services = new ServiceCollection()
                               .AddSingleton<IConfiguration>(new ConfigurationBuilder().Build())
                               .AddLogging()
                               .AddInfrastructure()
                               .AddSqlServerProvider()
                               .BuildServiceProvider();

    Should.Throw<InvalidOperationException>(() => services.GetRequiredService<IJobClaimDialect>())
          .Message.ShouldContain("AddPostgreSqlProvider()");
  }

  [Fact]
  public void AddProviders_WithNullServices_ShouldThrowArgumentNullException()
  {
    Should.Throw<ArgumentNullException>(() => PostgreSql.DependencyInjection.AddPostgreSqlProvider(null!));
    Should.Throw<ArgumentNullException>(() => SqlServer.DependencyInjection.AddSqlServerProvider(null!));
  }

  [Fact]
  public void AddProviders_CalledTwice_ShouldRegisterOneDialectEach()
  {
    var services = new ServiceCollection().AddPostgreSqlProvider()
                                          .AddPostgreSqlProvider()
                                          .AddSqlServerProvider()
                                          .AddSqlServerProvider();

    services.Count(d => d.ServiceType == typeof(IJobClaimDialect)).ShouldBe(2);
  }

  [Fact]
  public async Task AddInfrastructure_ShouldRegisterTheReadinessChecks()
  {
    await using var services = Build(new Dictionary<string, string?>());

    var ready = services.GetRequiredService<IOptions<HealthCheckServiceOptions>>()
                        .Value.Registrations
                        .Where(r => r.Tags.Contains(DependencyInjection.ReadyTag))
                        .Select(r => r.Name);

    ready.ShouldBe(["database", "migrations", "read-committed-snapshot"], ignoreOrder: true);
  }

  [Fact]
  public async Task AddInfrastructure_ShouldNotRegisterTheMigrator()
  {
    await using var services = Build(new Dictionary<string, string?>());

    services.GetServices<IHostedService>().OfType<DatabaseMigrator>().ShouldBeEmpty();
  }

  // Both hosts resolve the meter from AddInfrastructure, with the meter factory of the host.
  [Fact]
  public async Task AddInfrastructure_ShouldRegisterTheMeterAsSingleton()
  {
    await using var services = Build(new Dictionary<string, string?>());

    services.GetRequiredService<CodeCoverageMeter>().ShouldBeSameAs(services.GetRequiredService<CodeCoverageMeter>());
  }

  // The Web calls AddInfrastructure without AddJobProcessing: it queues jobs, it never runs them.
  [Fact]
  public async Task AddInfrastructure_ShouldNotRegisterTheJobWorker()
  {
    await using var services = Build(new Dictionary<string, string?>());

    var hostedServices = services.GetServices<IHostedService>().ToList();

    hostedServices.OfType<JobWorker>().ShouldBeEmpty();
    hostedServices.OfType<LeaseSweeper>().ShouldBeEmpty();
    hostedServices.OfType<RecurringJobScheduler>().ShouldBeEmpty();
    hostedServices.OfType<JobQueueMetricsSampler>().ShouldBeEmpty();
    services.GetRequiredService<JobWakeUpSignal>().ShouldBeSameAs(services.GetRequiredService<JobWakeUpSignal>());
  }

  [Theory]
  [InlineData(null, true)]
  [InlineData("true", true)]
  [InlineData("false", false)]
  public async Task AddJobProcessing_ShouldRegisterTheWorkerAndTheSweeper(string? enabled, bool expected)
  {
    var settings = new Dictionary<string, string?> { ["ConnectionStrings:CodeCoverage"] = "Server=localhost;Database=unused" };

    // Without the key, the default applies (a null value would bind as false).
    if (enabled is not null)
    {
      settings["Worker:Enabled"] = enabled;
    }

    var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
    await using var services = new ServiceCollection()
                               .AddSingleton<IConfiguration>(configuration)
                               .AddLogging()
                               .AddApplication()
                               .AddInfrastructure()
                               .AddPostgreSqlProvider()
                               .AddSqlServerProvider()
                               .AddJobProcessing()
                               // Registered with the key ring of the host (AddCodeCoverageDataProtection), not here.
                               .AddSingleton(A.Fake<ISecretProtector>())
                               .BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
    await using var scope = services.CreateAsyncScope();

    var hostedServices = services.GetServices<IHostedService>().ToList();

    hostedServices.OfType<JobWorker>().ShouldHaveSingleItem();
    hostedServices.OfType<LeaseSweeper>().ShouldHaveSingleItem();
    hostedServices.OfType<RecurringJobScheduler>().ShouldHaveSingleItem().Definitions.Count.ShouldBe(RecurringJobCatalog.All.Count);
    hostedServices.OfType<JobQueueMetricsSampler>().ShouldHaveSingleItem("the queue is measured even when the worker is idle");
    services.GetRequiredService<IOptions<WorkerOptions>>().Value.Enabled.ShouldBe(expected);
    scope.ServiceProvider.GetRequiredService<IJobDispatcher>().ShouldBeOfType<JobDispatcher>();
  }

  [Fact]
  public void AddJobProcessing_WithNullServices_ShouldThrowArgumentNullException()
  {
    Should.Throw<ArgumentNullException>(() => DependencyInjection.AddJobProcessing(null!));
  }

  [Fact]
  public async Task AddInfrastructure_ShouldRegisterTheSettingsChangeWatcher()
  {
    await using var services = Build(new Dictionary<string, string?>());

    services.GetServices<IHostedService>().OfType<SettingsChangeWatcher>().ShouldHaveSingleItem();
    services.GetRequiredService<IOptions<SettingsWatcherOptions>>().Value.PollSeconds.ShouldBe(5);
  }

  [Fact]
  public async Task AddInfrastructure_ShouldRegisterOneGitHubAppTokenProviderForTheProcess()
  {
    await using var services = Build(new Dictionary<string, string?>());

    var provider = services.GetRequiredService<GitHubAppTokenProvider>();

    services.GetRequiredService<GitHubAppTokenProvider>().ShouldBeSameAs(provider);
    services.GetRequiredService<IHttpClientFactory>().CreateClient(GitHubHttpClientConfiguration.ClientName)
            .BaseAddress.ShouldBe(new Uri("https://api.github.com/"));
  }

  private static ServiceProvider Build(Dictionary<string, string?> settings, bool withProviders = false)
  {
    var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
    var services = new ServiceCollection()
                   .AddSingleton<IConfiguration>(configuration)
                   .AddSingleton(A.Fake<ISecretProtector>())
                   .AddLogging()
                   .AddInfrastructure();

    if (withProviders)
    {
      services.AddPostgreSqlProvider().AddSqlServerProvider();
    }

    return services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
  }

  #endregion
}
