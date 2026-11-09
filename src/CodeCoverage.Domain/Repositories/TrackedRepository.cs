// ------------------------------------------------------------------------------------------------
// <copyright file="TrackedRepository.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Git;
using CodeCoverage.Domain.Guards;

namespace CodeCoverage.Domain.Repositories;

/// <summary>A repository of a forge whose coverage is tracked by one organization.</summary>
/// <remarks>
///   <para>
///     The identifier the forge gives the repository is its stable identity: a renaming changes the slug and
///     keeps the row. The same repository can have a row in two organizations, after a transfer for instance.
///   </para>
///   <para>
///     <see cref="Owner" /> is a copy for display, never used to resolve, authorize or reach the repository
///     (ADR-013).
///   </para>
/// </remarks>
public sealed class TrackedRepository
{
  #region Fields

  /// <summary>The maximum length of <see cref="ExternalId" />; a braced GUID takes 38 characters.</summary>
  public const int ExternalIdMaxLength = 40;

  /// <summary>The maximum length of <see cref="Namespace" />, which keeps the unique index within SQL Server's limit.</summary>
  public const int NamespaceMaxLength = 512;

  /// <summary>The maximum length of <see cref="Name" />, the longest path that GitLab allows.</summary>
  public const int NameMaxLength = 255;

  #endregion

  #region Ctors

  /// <summary>Initializes a new instance of the <see cref="TrackedRepository" /> class.</summary>
  /// <param name="installationId">The installation that gives access to the repository.</param>
  /// <param name="externalId">The identifier given by the forge, in lower case.</param>
  /// <param name="owner">The display copy of the owner.</param>
  /// <param name="namespace">The levels between the owner and the repository.</param>
  /// <param name="normalizedNamespace">The lower-case form of the namespace.</param>
  /// <param name="name">The name of the repository.</param>
  /// <param name="normalizedName">The lower-case form of the name.</param>
  /// <param name="defaultBranch">The default branch.</param>
  /// <param name="isPrivate">A value indicating whether the repository is private.</param>
  /// <param name="isActive">A value indicating whether the repository is tracked.</param>
  /// <param name="badgeTokenProtected">The protected badge token.</param>
  /// <param name="createdAt">When the row was created, in UTC.</param>
  /// <param name="updatedAt">When the row last changed, in UTC.</param>
  private TrackedRepository(
    long installationId,
    string externalId,
    string owner,
    string @namespace,
    string normalizedNamespace,
    string name,
    string normalizedName,
    string defaultBranch,
    bool isPrivate,
    bool isActive,
    string badgeTokenProtected,
    DateTimeOffset createdAt,
    DateTimeOffset updatedAt)
  {
    InstallationId = installationId;
    ExternalId = externalId;
    Owner = owner;
    Namespace = @namespace;
    NormalizedNamespace = normalizedNamespace;
    Name = name;
    NormalizedName = normalizedName;
    DefaultBranch = defaultBranch;
    IsPrivate = isPrivate;
    IsActive = isActive;
    BadgeTokenProtected = badgeTokenProtected;
    CreatedAt = createdAt;
    UpdatedAt = updatedAt;
  }

  #endregion

  #region Properties

  /// <summary>Gets the identifier of the row, assigned when it is saved.</summary>
  public long Id { get; private set; }

  /// <summary>Gets the identifier of the GitHub installation that gives access to the repository.</summary>
  public long InstallationId { get; private set; }

  /// <summary>Gets the identifier the forge gives the repository, in lower case, which survives a renaming.</summary>
  public string ExternalId { get; private set; }

  /// <summary>Gets the owner as its forge spells it, a copy for display only.</summary>
  public string Owner { get; private set; }

  /// <summary>
  ///   Gets the levels between the owner and the repository, written <c>sub/sub2</c>, or an empty text on a
  ///   forge that has none.
  /// </summary>
  public string Namespace { get; private set; }

  /// <summary>Gets the lower-case form of <see cref="Namespace" />.</summary>
  public string NormalizedNamespace { get; private set; }

  /// <summary>Gets the name of the repository as its forge spells it.</summary>
  public string Name { get; private set; }

  /// <summary>Gets the lower-case form of <see cref="Name" />, unique in its namespace.</summary>
  public string NormalizedName { get; private set; }

  /// <summary>Gets the default branch of the repository.</summary>
  public string DefaultBranch { get; private set; }

  /// <summary>Gets a value indicating whether the repository is private on its forge.</summary>
  public bool IsPrivate { get; private set; }

  /// <summary>Gets a value indicating whether the coverage of the repository is tracked.</summary>
  public bool IsActive { get; private set; }

  /// <summary>Gets the badge token, protected by Data Protection.</summary>
  public string BadgeTokenProtected { get; private set; }

  /// <summary>Gets the configuration read from the default branch, or <see langword="null" />.</summary>
  public string? DefaultBranchYaml { get; private set; }

  /// <summary>Gets the commit <see cref="DefaultBranchYaml" /> was read from, or <see langword="null" />.</summary>
  public GitSha? DefaultBranchYamlSha { get; private set; }

  /// <summary>Gets when the row was created, in UTC.</summary>
  public DateTimeOffset CreatedAt { get; private set; }

  /// <summary>Gets when the row last changed, in UTC.</summary>
  public DateTimeOffset UpdatedAt { get; private set; }

  /// <summary>Gets the version of the row, incremented by each change to detect concurrent writes.</summary>
  public int Version { get; private set; }

  #endregion

  #region Methods

  /// <summary>Starts tracking a repository.</summary>
  /// <param name="installationId">The GitHub installation that gives access to the repository.</param>
  /// <param name="externalId">The identifier the forge gives the repository.</param>
  /// <param name="slug">The current name of the repository.</param>
  /// <param name="defaultBranch">The default branch of the repository.</param>
  /// <param name="isPrivate">A value indicating whether the repository is private.</param>
  /// <param name="badgeTokenProtected">The badge token, already protected.</param>
  /// <param name="at">The current time; converted to UTC.</param>
  /// <returns>The active repository, not saved yet.</returns>
  /// <exception cref="ArgumentOutOfRangeException"><paramref name="installationId" /> is not positive.</exception>
  /// <exception cref="ArgumentNullException">
  ///   <paramref name="externalId" />, <paramref name="slug" />, <paramref name="defaultBranch" /> or
  ///   <paramref name="badgeTokenProtected" /> is <see langword="null" />.
  /// </exception>
  /// <exception cref="ArgumentException">
  ///   <paramref name="externalId" />, <paramref name="defaultBranch" /> or
  ///   <paramref name="badgeTokenProtected" /> is empty, white space or too long.
  /// </exception>
  public static TrackedRepository Create(
    long installationId,
    string externalId,
    RepositorySlug slug,
    string defaultBranch,
    bool isPrivate,
    string badgeTokenProtected,
    DateTimeOffset at)
  {
    ArgumentOutOfRangeException.ThrowIfNegativeOrZero(installationId);
    ArgumentException.ThrowIfNullOrWhiteSpace(externalId);
    ArgumentNullException.ThrowIfNull(slug);
    ArgumentException.ThrowIfNullOrWhiteSpace(defaultBranch);
    ArgumentException.ThrowIfNullOrWhiteSpace(badgeTokenProtected);

    ArgumentException.ThrowIf(
      externalId.Length > ExternalIdMaxLength,
      $"The value cannot be longer than {ExternalIdMaxLength} characters.",
      nameof(externalId));

    ArgumentException.ThrowIf(
      defaultBranch.Length > Branch.NameMaxLength,
      $"The value cannot be longer than {Branch.NameMaxLength} characters.",
      nameof(defaultBranch));

    var utc = at.ToUniversalTime();

    return new TrackedRepository(
      installationId,
      NormalizeExternalId(externalId),
      slug.Owner,
      slug.Namespace,
      slug.NormalizedNamespace,
      slug.Name,
      slug.NormalizedName,
      defaultBranch,
      isPrivate,
      isActive: true,
      badgeTokenProtected,
      utc,
      utc);
  }

  /// <summary>Gives the form of an external identifier that is compared and stored as unique.</summary>
  /// <param name="externalId">The identifier given by the forge.</param>
  /// <returns>The identifier in lower case.</returns>
  /// <remarks>
  ///   The unique index then behaves the same under the collations of both database engines, one of which
  ///   ignores the case.
  /// </remarks>
  /// <exception cref="ArgumentNullException"><paramref name="externalId" /> is <see langword="null" />.</exception>
  public static string NormalizeExternalId(string externalId)
  {
    ArgumentNullException.ThrowIfNull(externalId);

    return externalId.ToLowerInvariant();
  }

  /// <summary>Applies the name that the forge reports, which keeps the identity of the repository.</summary>
  /// <param name="newSlug">The new name of the repository.</param>
  /// <param name="at">The current time; converted to UTC.</param>
  /// <exception cref="ArgumentNullException"><paramref name="newSlug" /> is <see langword="null" />.</exception>
  public void Rename(RepositorySlug newSlug, DateTimeOffset at)
  {
    ArgumentNullException.ThrowIfNull(newSlug);

    if (string.Equals(newSlug.Owner, Owner, StringComparison.Ordinal)
        && string.Equals(newSlug.Namespace, Namespace, StringComparison.Ordinal)
        && string.Equals(newSlug.Name, Name, StringComparison.Ordinal))
    {
      return;
    }

    Owner = newSlug.Owner;
    Namespace = newSlug.Namespace;
    NormalizedNamespace = newSlug.NormalizedNamespace;
    Name = newSlug.Name;
    NormalizedName = newSlug.NormalizedName;
    UpdatedAt = at.ToUniversalTime();
    Version++;
  }

  #endregion
}
