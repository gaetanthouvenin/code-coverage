// ------------------------------------------------------------------------------------------------
// <copyright file="BoundedSourceContentCache.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Globalization;

using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

using CodeCoverage.Domain.Contracts.Forges;
using CodeCoverage.Domain.Git;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Infrastructure.GitHub;

namespace CodeCoverage.Infrastructure.Caching;

/// <summary>
///   Reads the sources of the annotated source page from their forge, keeping them 10 minutes in a memory bounded to
///   about 100 MB, under a key that starts with the organization of the scope.
/// </summary>
/// <param name="memory">The bounded memory, shared by the process.</param>
/// <param name="organization">The organization of the scope.</param>
/// <param name="forges">The gateways of the forges.</param>
/// <param name="logger">The logger.</param>
/// <remarks>
///   A file the forge does not have, one larger than 1 MB, a binary one (a NUL character) and a forge with no adapter are
///   « unavailable », and that verdict is kept like a text, so a second view never reads again. A rate limit is
///   « unavailable » too, but never kept: the quota comes back soon. Any other refusal of the forge is raised.
/// </remarks>
internal sealed partial class BoundedSourceContentCache(
  SourceContentMemory memory,
  ICurrentOrganization organization,
  IForgeGatewayProvider forges,
  ILogger<BoundedSourceContentCache> logger) : ISourceContentCache
{
  #region Fields

  /// <summary>The largest source read, in bytes.</summary>
  public const int MaxBytes = 1024 * 1024;

  // Cannot appear in a path nor in an identifier: two files never give the same key.
  private const char Separator = '\u001f';

  private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(10);

  #endregion

  #region Implementation of ISourceContentCache

  /// <inheritdoc />
  public async Task<string?> GetSourceAsync(ForgeRepositoryRef repository, GitSha sha, string path, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(repository);
    ArgumentNullException.ThrowIfNull(sha);
    ArgumentNullException.ThrowIfNull(path);

    var key = string.Join(
      Separator,
      "source",
      organization.Id.ToString(CultureInfo.InvariantCulture),
      repository.Forge.ToString(),
      repository.ExternalId,
      sha.Value,
      path
    );

    if (memory.Cache.TryGetValue(key, out CachedSource? cached) && cached is not null)
    {
      return cached.Text;
    }

    string? text;

    try
    {
      text = await ReadAsync(repository, sha, path, cancellationToken).ConfigureAwait(false);
    }
    catch (GitHubRateLimitedException exception)
    {
      // Not kept: the quota comes back within the hour, and the next view reads again.
      LogRateLimited(exception.ResetAt);
      return null;
    }

    // A text costs its characters; a verdict costs one.
    memory.Cache.Set(key, new CachedSource(text), new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = Lifetime, Size = Math.Max(1, text?.Length ?? 0) });

    return text;
  }

  #endregion

  #region Methods

  private async Task<string?> ReadAsync(ForgeRepositoryRef repository, GitSha sha, string path, CancellationToken cancellationToken)
  {
    IForgeGateway gateway;

    try
    {
      gateway = forges.GetGateway(repository.Forge);
    }
    catch (NotSupportedException)
    {
      // A forge prepared by ADR-014 with no adapter yet: nothing to read.
      return null;
    }

    var content = await gateway.GetBoundedContentAsync(repository, path, sha, MaxBytes, cancellationToken).ConfigureAwait(false);

    return content is { Text: { } text } && !text.Contains('\0', StringComparison.Ordinal) ? text : null;
  }

  [LoggerMessage(Level = LogLevel.Information, Message = "The forge rate limited the reading of a source until {ResetAt}: shown unavailable.")]
  private partial void LogRateLimited(DateTimeOffset resetAt);

  #endregion
}
