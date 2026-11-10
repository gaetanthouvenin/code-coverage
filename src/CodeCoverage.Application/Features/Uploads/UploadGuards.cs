// ------------------------------------------------------------------------------------------------
// <copyright file="UploadGuards.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Diagnostics.CodeAnalysis;

using CodeCoverage.Application.Exceptions;
using CodeCoverage.Domain.Commits;

namespace CodeCoverage.Application.Features.Uploads;

/// <summary>
///   The refusals of the uploads, in the form of
///   <see cref="ArgumentNullException.ThrowIfNull(object?, string?)" />: one place holds the code and the
///   message of each.
/// </summary>
internal static class UploadGuards
{
  #region Methods

  extension(NotFoundException)
  {
    /// <summary>Throws when an upload was not found in a repository.</summary>
    /// <param name="found">Whether the upload exists and belongs to the repository that was named.</param>
    /// <exception cref="NotFoundException"><paramref name="found" /> is <see langword="false" />.</exception>
    public static void ThrowIfUploadNotFound([DoesNotReturnIf(false)] bool found)
    {
      if (!found)
      {
        throw new NotFoundException(UploadErrors.NotFound, "The upload does not exist in this repository.");
      }
    }
  }

  extension(ConflictException)
  {
    /// <summary>Throws when an upload is not failed: only a failed upload is retried.</summary>
    /// <param name="state">The state of the upload.</param>
    /// <exception cref="ConflictException"><paramref name="state" /> is not <see cref="UploadState.Failed" />.</exception>
    public static void ThrowIfUploadNotFailed(UploadState state)
    {
      if (state is not UploadState.Failed)
      {
        throw new ConflictException(UploadErrors.NotFailed, $"The upload is {state}: only a failed upload is retried.");
      }
    }
  }

  extension(UnprocessableEntityException)
  {
    /// <summary>Throws when the raw files of an upload are no longer kept.</summary>
    /// <param name="expired">Whether the upload can no longer be parsed again.</param>
    /// <exception cref="UnprocessableEntityException"><paramref name="expired" /> is <see langword="true" />.</exception>
    public static void ThrowIfUploadRawExpired([DoesNotReturnIf(true)] bool expired)
    {
      if (expired)
      {
        throw new UnprocessableEntityException(UploadErrors.RawExpired, "The raw files of this upload are no longer kept.");
      }
    }
  }

  #endregion
}
