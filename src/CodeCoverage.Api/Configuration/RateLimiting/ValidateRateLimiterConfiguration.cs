// ------------------------------------------------------------------------------------------------
// <copyright file="ValidateRateLimiterConfiguration.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Globalization;
using System.Threading.RateLimiting;

using Microsoft.AspNetCore.RateLimiting;

namespace CodeCoverage.Api.Configuration.RateLimiting;

/// <summary>Limits the anonymous validation of a <c>codecov.yml</c> file, per client address.</summary>
public static class ValidateRateLimiterConfiguration
{
  #region Fields

  /// <summary>The name of the rate limiting policy of the endpoint.</summary>
  public const string PolicyName = "validate";

  /// <summary>The number of requests a client address may send per window.</summary>
  /// <remarks>
  ///   A constant: no setting of the catalog carries this limit yet, and Codecov has no limit of its own
  ///   on <c>/validate</c> to follow.
  /// </remarks>
  internal const int PermitLimit = 60;

  private const string UnknownClient = "unknown";

  private static readonly TimeSpan Window = TimeSpan.FromMinutes(1);

  #endregion

  #region Methods

  /// <summary>Registers the policy, and answers 429 with a <c>Retry-After</c> header once it refuses.</summary>
  /// <param name="services">The service collection.</param>
  /// <returns>The same collection, for chaining.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="services" /> is <see langword="null" />.</exception>
  public static IServiceCollection AddValidateRateLimiter(this IServiceCollection services)
  {
    ArgumentNullException.ThrowIfNull(services);

    return services.AddRateLimiter(options =>
    {
      options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
      options.OnRejected = WriteRejectionAsync;
      options.AddPolicy(PolicyName, CreatePartition);
    });
  }

  // The address comes from the known reverse proxies only (ForwardedHeadersConfiguration).
  private static RateLimitPartition<string> CreatePartition(HttpContext context) =>
    RateLimitPartition.GetFixedWindowLimiter(
      context.Connection.RemoteIpAddress?.ToString() ?? UnknownClient,
      _ => new FixedWindowRateLimiterOptions { PermitLimit = PermitLimit, Window = Window, QueueLimit = 0 });

  private static async ValueTask WriteRejectionAsync(OnRejectedContext context, CancellationToken cancellationToken)
  {
    var httpContext = context.HttpContext;

    if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
    {
      httpContext.Response.Headers.RetryAfter = ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString(CultureInfo.InvariantCulture);
    }

    var problemDetails = httpContext.RequestServices.GetRequiredService<IProblemDetailsService>();

    await problemDetails.TryWriteAsync(
                          new ProblemDetailsContext
                          {
                            HttpContext = httpContext,
                            ProblemDetails =
                            {
                              Status = StatusCodes.Status429TooManyRequests,
                              Title = "Too many requests",
                            },
                          })
                        .ConfigureAwait(false);
  }

  #endregion
}
