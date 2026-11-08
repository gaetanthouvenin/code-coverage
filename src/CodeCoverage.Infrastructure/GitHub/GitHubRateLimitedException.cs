// ------------------------------------------------------------------------------------------------
// <copyright file="GitHubRateLimitedException.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Globalization;

namespace CodeCoverage.Infrastructure.GitHub;

/// <summary>Exception thrown when GitHub refuses a request because of a primary or a secondary rate limit.</summary>
/// <remarks>
///   It is not a failure of the work that sent the request, which only has to wait until <see cref="ResetAt" />: it
///   carries no error code and is not an <see cref="CodeCoverage.Application.Exceptions.ApplicationException" />.
/// </remarks>
public sealed class GitHubRateLimitedException : Exception
{
  #region Ctors

  /// <summary>Initializes a new instance of the <see cref="GitHubRateLimitedException" /> class.</summary>
  /// <param name="resetAt">The moment from which GitHub accepts requests again.</param>
  public GitHubRateLimitedException(DateTimeOffset resetAt)
    : base($"GitHub rate limited the request until {resetAt.ToString("O", CultureInfo.InvariantCulture)}.")
  {
    ResetAt = resetAt;
  }

  #endregion

  #region Properties

  /// <summary>Gets the moment from which GitHub accepts requests again.</summary>
  public DateTimeOffset ResetAt { get; }

  #endregion
}
