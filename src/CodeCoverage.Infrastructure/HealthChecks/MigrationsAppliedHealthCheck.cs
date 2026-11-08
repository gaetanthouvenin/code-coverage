// ------------------------------------------------------------------------------------------------
// <copyright file="MigrationsAppliedHealthCheck.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

using CodeCoverage.Infrastructure.Persistence;

namespace CodeCoverage.Infrastructure.HealthChecks;

/// <summary>Reports whether every migration of the configured provider is applied to the database.</summary>
/// <param name="dbContext">The context of the database to check.</param>
/// <remarks>
///   Part of the readiness probe of both hosts: a host whose code expects a newer schema than the
///   database holds is not ready. The Web never migrates, so it stays not ready until the Api has.
/// </remarks>
internal sealed class MigrationsAppliedHealthCheck(CodeCoverageDbContext dbContext) : IHealthCheck
{
  #region Fields

  /// <summary>The name of the check in the health report.</summary>
  public const string Name = "migrations";

  #endregion

  #region Implementation of IHealthCheck

  /// <inheritdoc />
  public async Task<HealthCheckResult> CheckHealthAsync(
    HealthCheckContext context,
    CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(context);

    var pending = (await dbContext.Database.GetPendingMigrationsAsync(cancellationToken).ConfigureAwait(false)).ToList();

    if (pending.Count == 0)
    {
      return HealthCheckResult.Healthy("Every migration is applied.");
    }

    return new HealthCheckResult(
      context.Registration.FailureStatus,
      $"{pending.Count} migration(s) not applied: {string.Join(", ", pending)}.",
      data: new Dictionary<string, object>(StringComparer.Ordinal) { ["pending"] = pending }
    );
  }

  #endregion
}
