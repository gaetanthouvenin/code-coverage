// ------------------------------------------------------------------------------------------------
// <copyright file="GitHubQuota.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Domain.Operations;

/// <summary>The GitHub quota of an installation, or of the application itself, as its last answer reported it.</summary>
/// <remarks>Operational data: an installation identifier, no organization, no business data.</remarks>
public sealed class GitHubQuota
{
  #region Fields

  /// <summary>The installation of the quota of the application itself.</summary>
  public const string AppInstallation = "app";

  /// <summary>The longest installation key.</summary>
  public const int InstallationMaxLength = 32;

  #endregion

  #region Ctors

  private GitHubQuota(string installation, long remaining, long? limit, DateTimeOffset? resetAt, DateTimeOffset observedAt)
  {
    Installation = installation;
    Remaining = remaining;
    Limit = limit;
    ResetAt = resetAt;
    ObservedAt = observedAt;
  }

  #endregion

  #region Properties

  /// <summary>Gets the installation, or <see cref="AppInstallation" /> for the application.</summary>
  public string Installation { get; private set; }

  /// <summary>Gets the requests left.</summary>
  public long Remaining { get; private set; }

  /// <summary>Gets the requests of the whole window, or <see langword="null" /> when GitHub did not say.</summary>
  public long? Limit { get; private set; }

  /// <summary>Gets when the window starts again, or <see langword="null" /> when GitHub did not say.</summary>
  public DateTimeOffset? ResetAt { get; private set; }

  /// <summary>Gets when GitHub reported it.</summary>
  public DateTimeOffset ObservedAt { get; private set; }

  #endregion

  #region Methods

  /// <summary>Creates a quota.</summary>
  /// <param name="installation">The installation, or <see cref="AppInstallation" />.</param>
  /// <param name="remaining">The requests left.</param>
  /// <param name="limit">The requests of the whole window, or <see langword="null" />.</param>
  /// <param name="resetAt">When the window starts again, or <see langword="null" />; converted to UTC.</param>
  /// <param name="observedAt">When GitHub reported it; converted to UTC.</param>
  /// <returns>The quota.</returns>
  /// <exception cref="ArgumentException"><paramref name="installation" /> is empty or longer than <see cref="InstallationMaxLength" />.</exception>
  public static GitHubQuota Create(string installation, long remaining, long? limit, DateTimeOffset? resetAt, DateTimeOffset observedAt)
  {
    ArgumentException.ThrowIfNullOrWhiteSpace(installation);
    ArgumentOutOfRangeException.ThrowIfGreaterThan(installation.Length, InstallationMaxLength, nameof(installation));

    return new GitHubQuota(installation, remaining, limit, resetAt?.ToUniversalTime(), observedAt.ToUniversalTime());
  }

  #endregion
}
