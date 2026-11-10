// ------------------------------------------------------------------------------------------------
// <copyright file="SerilogConfiguration.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Globalization;

using Serilog;
using Serilog.Events;
using Serilog.Formatting.Compact;

using ILogger = Serilog.ILogger;

namespace CodeCoverage.Api.Configuration;

/// <summary>Configures Serilog: console and local rolling file.</summary>
/// <remarks>Every event carries <c>Application</c>, the machine name and the thread identifier.</remarks>
public static class SerilogConfiguration
{
  #region Fields

  /// <summary>The application name every event carries.</summary>
  public const string ApplicationName = "CodeCoverage.Api";

  /// <summary>The configuration key of the rolling log file, relative to the content root.</summary>
  public const string FilePathKey = "Logging:FilePath";

  /// <summary>
  ///   The configuration key that sends the console events to the standard error, set by the
  ///   administration command line, whose standard output holds the result of the command.
  /// </summary>
  public const string ConsoleToStandardErrorKey = "Logging:ConsoleToStandardError";

  private const string DefaultFilePath = "logs/codecoverage-api-.log";

  private const long FileSizeLimitBytes = 100L * 1024 * 1024;

  private const int RetainedFileCountLimit = 31;

  #endregion

  #region Methods

  /// <summary>Creates the bootstrap logger used before the host is built.</summary>
  /// <param name="toStandardError">Whether the events go to the standard error instead of the standard output.</param>
  /// <returns>The bootstrap logger.</returns>
  public static ILogger CreateBootstrapLogger(bool toStandardError = false)
  {
    return new LoggerConfiguration().MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
                                    .WriteTo.Console(formatProvider: CultureInfo.InvariantCulture, standardErrorFromLevel: StandardErrorFrom(toStandardError))
                                    .CreateBootstrapLogger();
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

    var standardErrorFrom = StandardErrorFrom(settings.GetValue<bool>(ConsoleToStandardErrorKey));

    if (context.HostingEnvironment.IsDevelopment())
    {
      configuration.WriteTo.Console(
        outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext}{NewLine}{Message:lj}{NewLine}{Exception}",
        formatProvider: CultureInfo.InvariantCulture,
        standardErrorFromLevel: standardErrorFrom
      );
    }
    else
    {
      configuration.WriteTo.Console(new CompactJsonFormatter(), standardErrorFromLevel: standardErrorFrom);
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

  private static LogEventLevel? StandardErrorFrom(bool toStandardError) => toStandardError ? LogEventLevel.Verbose : null;

  #endregion
}
