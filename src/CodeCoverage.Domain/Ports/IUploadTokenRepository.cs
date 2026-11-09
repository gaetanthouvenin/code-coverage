// ------------------------------------------------------------------------------------------------
// <copyright file="IUploadTokenRepository.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Tokens;

namespace CodeCoverage.Domain.Ports;

/// <summary>The upload tokens of the organization of the scope.</summary>
public interface IUploadTokenRepository
{
  #region Methods

  /// <summary>Adds a token, saved at once, unless its value is already known in the organization.</summary>
  /// <param name="token">The token.</param>
  /// <param name="cancellationToken">A token to cancel the saving.</param>
  /// <returns><see langword="true" /> when it was added; <see langword="false" /> when the same value already exists.</returns>
  Task<bool> TryAddAsync(UploadToken token, CancellationToken cancellationToken);

  /// <summary>Finds a token, which can then be updated.</summary>
  /// <param name="id">The identifier of the token.</param>
  /// <param name="cancellationToken">A token to cancel the reading.</param>
  /// <returns>The token, or <see langword="null" />.</returns>
  Task<UploadToken?> FindAsync(long id, CancellationToken cancellationToken);

  /// <summary>Lists the tokens of the organization itself, the most recent first.</summary>
  /// <param name="cancellationToken">A token to cancel the reading.</param>
  /// <returns>The tokens.</returns>
  Task<IReadOnlyList<UploadToken>> ListOfOrganizationAsync(CancellationToken cancellationToken);

  /// <summary>Lists the tokens of a repository, the most recent first.</summary>
  /// <param name="repositoryId">The identifier of the repository.</param>
  /// <param name="cancellationToken">A token to cancel the reading.</param>
  /// <returns>The tokens.</returns>
  Task<IReadOnlyList<UploadToken>> ListOfRepositoryAsync(long repositoryId, CancellationToken cancellationToken);

  /// <summary>Saves the changes of a token read in this scope.</summary>
  /// <param name="token">The token.</param>
  /// <param name="cancellationToken">A token to cancel the saving.</param>
  /// <returns>A task that completes when it is saved.</returns>
  Task UpdateAsync(UploadToken token, CancellationToken cancellationToken);

  #endregion
}
