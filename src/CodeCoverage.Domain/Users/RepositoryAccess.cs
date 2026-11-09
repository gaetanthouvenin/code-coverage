// ------------------------------------------------------------------------------------------------
// <copyright file="RepositoryAccess.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Guards;
using CodeCoverage.Domain.Repositories;

namespace CodeCoverage.Domain.Users;

/// <summary>What a user may do on a repository, as the forge reported it when last asked.</summary>
/// <remarks>
///   The repository is named by the identifier its forge gives it, without a reference to a tracked repository: the
///   cache can cite a repository that is not tracked yet.
/// </remarks>
public sealed class RepositoryAccess
{
  #region Ctors

  /// <summary>Initializes a new instance of the <see cref="RepositoryAccess" /> class.</summary>
  /// <param name="userId">The identifier of the user.</param>
  /// <param name="repositoryExternalId">The identifier the forge gives the repository, in lower case.</param>
  /// <param name="permission">The access level.</param>
  /// <param name="refreshedAt">When the forge was asked, in UTC.</param>
  private RepositoryAccess(long userId, string repositoryExternalId, RepositoryPermission permission, DateTimeOffset refreshedAt)
  {
    UserId = userId;
    RepositoryExternalId = repositoryExternalId;
    Permission = permission;
    RefreshedAt = refreshedAt;
  }

  #endregion

  #region Properties

  /// <summary>Gets the identifier of the row, assigned when it is saved.</summary>
  public long Id { get; private set; }

  /// <summary>Gets the identifier of the user.</summary>
  public long UserId { get; private set; }

  /// <summary>Gets the identifier the forge gives the repository, in lower case.</summary>
  public string RepositoryExternalId { get; private set; }

  /// <summary>Gets the access level.</summary>
  public RepositoryPermission Permission { get; private set; }

  /// <summary>Gets when the forge was last asked, in UTC.</summary>
  public DateTimeOffset RefreshedAt { get; private set; }

  #endregion

  #region Methods

  /// <summary>Records what the forge reports for a repository.</summary>
  /// <param name="userId">The identifier of the user.</param>
  /// <param name="repositoryExternalId">The identifier the forge gives the repository.</param>
  /// <param name="permission">The access level.</param>
  /// <param name="at">The current time; converted to UTC.</param>
  /// <returns>The access, not saved yet.</returns>
  /// <exception cref="ArgumentOutOfRangeException"><paramref name="userId" /> is not positive, or <paramref name="permission" /> is not defined.</exception>
  /// <exception cref="ArgumentNullException"><paramref name="repositoryExternalId" /> is <see langword="null" />.</exception>
  /// <exception cref="ArgumentException"><paramref name="repositoryExternalId" /> is empty, white space or too long.</exception>
  public static RepositoryAccess Create(long userId, string repositoryExternalId, RepositoryPermission permission, DateTimeOffset at)
  {
    ArgumentOutOfRangeException.ThrowIfNegativeOrZero(userId);
    ArgumentException.ThrowIfNullOrWhiteSpace(repositoryExternalId);
    ArgumentException.ThrowIf(
      repositoryExternalId.Length > TrackedRepository.ExternalIdMaxLength,
      $"The value cannot be longer than {TrackedRepository.ExternalIdMaxLength} characters.",
      nameof(repositoryExternalId)
    );
    ArgumentOutOfRangeException.ThrowIfUndefined(permission);

    return new RepositoryAccess(userId, TrackedRepository.NormalizeExternalId(repositoryExternalId), permission, at.ToUniversalTime());
  }

  /// <summary>Applies what the forge reports now.</summary>
  /// <param name="permission">The access level.</param>
  /// <param name="at">The current time; converted to UTC.</param>
  /// <exception cref="ArgumentOutOfRangeException"><paramref name="permission" /> is not defined.</exception>
  public void Refresh(RepositoryPermission permission, DateTimeOffset at)
  {
    ArgumentOutOfRangeException.ThrowIfUndefined(permission);

    Permission = permission;
    RefreshedAt = at.ToUniversalTime();
  }

  #endregion
}
