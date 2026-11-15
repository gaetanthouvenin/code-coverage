// ------------------------------------------------------------------------------------------------
// <copyright file="SerilogConfiguration.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Globalization;

using Serilog;
using Serilog.Events;
using Serilog.Extensions.Hosting;
using Serilog.Formatting.Compact;

namespace CodeCoverage.Web.Configuration;

/// <summary>Configures Serilog: console and local rolling file.</summary>
/// <remarks>Every event carries <c>Application</c>, the machine name and the thread identifier.</remarks>
public static class SerilogConfiguration
{
  #region Fields

  /// <summary>The application name every event carries.</summary>
  public const string ApplicationName = "CodeCoverage.Web";

  /// <summary>The configuration key of the rolling log file, relative to the content root.</summary>
  public const string FilePathKey = "Logging:FilePath";

  private const string DefaultFilePath = "logs/codecoverage-web-.log";

  private const long FileSizeLimitBytes = 100L * 1024 * 1024;

  private const int RetainedFileCountLimit = 31;

  // Serializes the registrations of the hosts of one process: see UseSerilog.
  private static readonly Lock RegistrationLock = new();

  #endregion

  #region Methods

  /// <summary>Creates the bootstrap logger used before the host is built.</summary>
  /// <returns>The bootstrap logger.</returns>
  public static ReloadableLogger CreateBootstrapLogger()
  {
    return new LoggerConfiguration().MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
                                    .WriteTo.Console(formatProvider: CultureInfo.InvariantCulture)
                                    .CreateBootstrapLogger();
  }

  /// <summary>
  ///   Sets Serilog as the logging provider of a host, which reloads then freezes its own bootstrap logger
  ///   with <see cref="ConfigureLogger" />.
  /// </summary>
  /// <param name="builder">The builder of the host.</param>
  /// <param name="bootstrapLogger">The bootstrap logger of this host, made by <see cref="CreateBootstrapLogger" />.</param>
  /// <remarks>
  ///   <para>
  ///     <c>AddSerilog</c> reloads, then freezes, the <see cref="ReloadableLogger" /> it reads from the static
  ///     <see cref="Log.Logger" /> at the moment it is called. Hosts built in parallel in one process
  ///     (<c>WebApplicationFactory</c>) replace that static logger, so two of them could freeze the same one:
  ///     « The logger is already frozen. ».
  ///   </para>
  ///   <para>
  ///     The registration puts the bootstrap logger of this host back in <see cref="Log.Logger" />, registers
  ///     Serilog, and starts again if another host replaced the static logger meanwhile: only this method
  ///     writes this bootstrap logger there, so finding it unchanged afterwards proves <c>AddSerilog</c> read it.
  ///     A single host registers exactly what <c>UseSerilog</c> registers.
  ///   </para>
  /// </remarks>
  /// <exception cref="ArgumentNullException">
  ///   <paramref name="builder" /> or <paramref name="bootstrapLogger" /> is <see langword="null" />.
  /// </exception>
  public static void UseSerilog(WebApplicationBuilder builder, ReloadableLogger bootstrapLogger)
  {
    ArgumentNullException.ThrowIfNull(builder);
    ArgumentNullException.ThrowIfNull(bootstrapLogger);

    // The context UseSerilog passes to ConfigureLogger.
    var context = new HostBuilderContext(((IHostBuilder)builder.Host).Properties)
    {
      Configuration = builder.Configuration,
      HostingEnvironment = builder.Environment,
    };

    lock (RegistrationLock)
    {
      while (true)
      {
        var registrations = new ServiceCollection();

        Log.Logger = bootstrapLogger;
        registrations.AddSerilog((services, configuration) => ConfigureLogger(context, services, configuration));

        if (ReferenceEquals(Log.Logger, bootstrapLogger))
        {
          foreach (var registration in registrations)
          {
            builder.Services.Add(registration);
          }

          return;
        }
      }
    }
  }

  /// <summary>Configures the application logger.</summary>
  /// <param name="context">The host builder context.</param>
  /// <param name="services">The service provider.</param>
  /// <param name="configuration">The logger configuration.</param>
  /// <exception cref="ArgumentNullException">
  ///   <paramref name="context" />, <paramref name="services" /> or <paramref name="configuration" />
  ///   is <see langword="null" />.
  /// </exception>
  public static void ConfigureLogger(
    HostBuilderContext context,
    IServiceProvider services,
    LoggerConfiguration configuration)
  {
    ArgumentNullException.ThrowIfNull(context);
    ArgumentNullException.ThrowIfNull(services);
    ArgumentNullException.ThrowIfNull(configuration);

    var settings = context.Configuration;

    // The framework is quiet by default; a "Serilog" section can still override these levels.
    configuration.MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
                 .MinimumLevel.Override("Microsoft.Hosting.Lifetime", LogEventLevel.Information)
                 .ReadFrom.Configuration(settings)
                 .ReadFrom.Services(services)
                 .Enrich.FromLogContext()
                 .Enrich.WithMachineName()
                 .Enrich.WithThreadId()
                 .Enrich.WithProperty("Application", ApplicationName);

    if (context.HostingEnvironment.IsDevelopment())
    {
      configuration.WriteTo.Console(
        outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext}{NewLine}{Message:lj}{NewLine}{Exception}",
        formatProvider: CultureInfo.InvariantCulture
      );
    }
    else
    {
      configuration.WriteTo.Console(new CompactJsonFormatter());
    }

    // A relative path is resolved against the content root, not the working directory.
    var filePath = settings[FilePathKey];

    configuration.WriteTo.File(
      new CompactJsonFormatter(),
      Path.GetFullPath(
        string.IsNullOrWhiteSpace(filePath) ? DefaultFilePath : filePath,
        context.HostingEnvironment.ContentRootPath
      ),
      rollingInterval: RollingInterval.Day,
      retainedFileCountLimit: RetainedFileCountLimit,
      fileSizeLimitBytes: FileSizeLimitBytes,
      rollOnFileSizeLimit: true
    );
  }

  #endregion
}
