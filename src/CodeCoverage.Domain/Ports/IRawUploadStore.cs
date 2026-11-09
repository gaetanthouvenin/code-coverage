// ------------------------------------------------------------------------------------------------
// <copyright file="IRawUploadStore.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Contracts.Uploads;

namespace CodeCoverage.Domain.Ports;

/// <summary>Stores the parts of the uploads as they were received, without holding them in memory.</summary>
/// <remarks>
///   An upload is named by its public identifier: its parts are stored before its row exists, which is written with its
///   job once every part is safe.
/// </remarks>
public interface IRawUploadStore
{
  #region Methods

  /// <summary>Writes a part of an upload, saved as it is read.</summary>
  /// <param name="uploadId">The public identifier of the upload.</param>
  /// <param name="part">The part.</param>
  /// <param name="cancellationToken">A token to cancel the saving.</param>
  /// <returns>Its SHA-256 and its sizes.</returns>
  /// <exception cref="UploadTooLargeException">The decompressed content of the upload exceeds a limit; nothing of the part is kept.</exception>
  Task<StoredRawFile> WriteAsync(Guid uploadId, UploadPart part, CancellationToken cancellationToken);

  /// <summary>Lists the parts of an upload, in the order they were written, each decompressed as it is read.</summary>
  /// <param name="uploadId">The public identifier of the upload.</param>
  /// <param name="cancellationToken">A token to cancel the reading.</param>
  /// <returns>The parts; their streams read the database of the scope, so read them one at a time.</returns>
  IAsyncEnumerable<UploadPart> OpenReadAsync(Guid uploadId, CancellationToken cancellationToken);

  /// <summary>Deletes the parts of an upload, if any.</summary>
  /// <param name="uploadId">The public identifier of the upload.</param>
  /// <param name="cancellationToken">A token to cancel the deletion.</param>
  /// <returns>A task that completes when they are deleted.</returns>
  Task DeleteAsync(Guid uploadId, CancellationToken cancellationToken);

  #endregion
}
