// ------------------------------------------------------------------------------------------------
// <copyright file="ForgeRateLimitedException.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Globalization;

using CodeCoverage.Domain.Organizations;

namespace CodeCoverage.Infrastructure.Forges;

/// <summary>Exception thrown when a forge refuses a request because of one of its rate limits.</summary>
/// <remarks>
///   It is not a failure of the work that sent the request, which only has to wait until <see cref="RetryAfter" />: it
///   carries no error code and is not an <see cref="CodeCoverage.Application.Exceptions.ApplicationException" />. The job
///   worker and the source content cache catch this type, whatever the forge; an adapter may raise a derived type.
/// </remarks>
public class ForgeRateLimitedException : Exception
{
  #region Ctors

  /// <summary>Initializes a new instance of the <see cref="ForgeRateLimitedException" /> class.</summary>
  /// <param name="forge">The forge that refused the request.</param>
  /// <param name="retryAfter">The moment from which the forge accepts requests again.</param>
  public ForgeRateLimitedException(Forge forge, DateTimeOffset retryAfter)
    : base($"{forge} rate limited the request until {retryAfter.ToString("O", CultureInfo.InvariantCulture)}.")
  {
    Forge = forge;
    RetryAfter = retryAfter;
  }

  #endregion

  #region Properties

  /// <summary>Gets the forge that refused the request.</summary>
  public Forge Forge { get; }

  /// <summary>Gets the moment from which the forge accepts requests again.</summary>
  public DateTimeOffset RetryAfter { get; }

  #endregion
}
