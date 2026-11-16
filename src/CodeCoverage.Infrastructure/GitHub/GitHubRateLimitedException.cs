// ------------------------------------------------------------------------------------------------
// <copyright file="GitHubRateLimitedException.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Organizations;
using CodeCoverage.Infrastructure.Forges;

namespace CodeCoverage.Infrastructure.GitHub;

/// <summary>Exception thrown when GitHub refuses a request because of a primary or a secondary rate limit.</summary>
/// <remarks>
///   A <see cref="ForgeRateLimitedException" /> of <see cref="Forge.GitHub" />: the job worker and the source content
///   cache catch the base type. The Api names it <see cref="GitHubErrors.RateLimited" /> when it is met during a request.
/// </remarks>
public sealed class GitHubRateLimitedException : ForgeRateLimitedException
{
  #region Ctors

  /// <summary>Initializes a new instance of the <see cref="GitHubRateLimitedException" /> class.</summary>
  /// <param name="resetAt">The moment from which GitHub accepts requests again.</param>
  public GitHubRateLimitedException(DateTimeOffset resetAt)
    : base(Forge.GitHub, resetAt)
  {
  }

  #endregion
}
