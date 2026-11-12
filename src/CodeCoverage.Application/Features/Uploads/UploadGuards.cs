// ------------------------------------------------------------------------------------------------
// <copyright file="UploadGuards.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Diagnostics.CodeAnalysis;
using System.Globalization;

using CodeCoverage.Application.Exceptions;
using CodeCoverage.Application.Exceptions.ErrorCodes;
using CodeCoverage.Domain.Commits;
using CodeCoverage.Domain.Contracts.Uploads;

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

  extension(BadRequestException)
  {
    /// <summary>Throws when an upload holds more reports than allowed.</summary>
    /// <param name="count">The number of reports read so far.</param>
    /// <param name="max">The number of reports an upload may hold.</param>
    /// <exception cref="BadRequestException"><paramref name="count" /> exceeds <paramref name="max" /> (<see cref="UploadErrors.TooManyFiles" />).</exception>
    public static void ThrowIfTooManyReports(int count, int max)
    {
      if (count > max)
      {
        throw new BadRequestException(UploadErrors.TooManyFiles, string.Create(CultureInfo.InvariantCulture, $"An upload holds at most {max} reports."));
      }
    }

    /// <summary>Throws when an upload holds a second network part (ARCHITECTURE §5.1).</summary>
    /// <param name="count">The number of network parts read so far.</param>
    /// <exception cref="BadRequestException"><paramref name="count" /> exceeds one (<see cref="UploadErrors.InvalidPart" />).</exception>
    public static void ThrowIfSecondNetworkPart(int count)
    {
      if (count > 1)
      {
        throw new BadRequestException(UploadErrors.InvalidPart, "An upload holds at most one network part.");
      }
    }

    /// <summary>Throws when the name of a part is too long.</summary>
    /// <param name="fileName">The name of the part.</param>
    /// <exception cref="ArgumentNullException"><paramref name="fileName" /> is <see langword="null" />.</exception>
    /// <exception cref="BadRequestException">The name is too long (<see cref="UploadErrors.InvalidPart" />).</exception>
    public static void ThrowIfPartNameTooLong(string fileName)
    {
      ArgumentNullException.ThrowIfNull(fileName);

      if (fileName.Length > UploadPart.FileNameMaxLength)
      {
        throw new BadRequestException(
          UploadErrors.InvalidPart,
          string.Create(CultureInfo.InvariantCulture, $"The name of a part cannot be longer than {UploadPart.FileNameMaxLength} characters.")
        );
      }
    }

    /// <summary>Throws when an upload holds no report.</summary>
    /// <param name="count">The number of reports read.</param>
    /// <exception cref="BadRequestException"><paramref name="count" /> is zero (<see cref="UploadErrors.NoReport" />).</exception>
    public static void ThrowIfNoReport(int count)
    {
      if (count == 0)
      {
        throw new BadRequestException(UploadErrors.NoReport, "An upload holds at least one report.");
      }
    }

    /// <summary>Refuses a part that is not valid gzip.</summary>
    /// <param name="inner">The failure of the decompression.</param>
    /// <exception cref="BadRequestException">Always (<see cref="UploadErrors.InvalidPart" />).</exception>
    [DoesNotReturn]
    public static void ThrowUploadNotGzip(InvalidDataException inner) =>
      throw new BadRequestException(UploadErrors.InvalidPart, "A part of the upload is not valid gzip.", inner);
  }

  extension(PayloadTooLargeException)
  {
    /// <summary>Refuses an upload whose content exceeds a limit.</summary>
    /// <param name="inner">The failure of the store.</param>
    /// <exception cref="PayloadTooLargeException">Always (<see cref="UploadErrors.TooLarge" />).</exception>
    [DoesNotReturn]
    public static void ThrowUploadTooLarge(Exception inner) =>
      throw new PayloadTooLargeException(UploadErrors.TooLarge, "The content of the upload exceeds a limit.", inner);
  }

  extension(ConflictException)
  {
    /// <summary>Throws when the upload a replay points to was removed in the meantime.</summary>
    /// <param name="existing">The upload of the first reception, or <see langword="null" />.</param>
    /// <exception cref="ConflictException"><paramref name="existing" /> is <see langword="null" /> (<c>General.ConcurrentChange</c>).</exception>
    public static void ThrowIfReplayedUploadRemoved([NotNull] Upload? existing)
    {
      if (existing is null)
      {
        throw new ConflictException(GeneralErrors.ConcurrentChange, "The upload was removed in the meantime; send it again.");
      }
    }
  }

  extension(ForbiddenException)
  {
    /// <summary>Throws when a repository upload token is used for another repository.</summary>
    /// <param name="otherRepository">Whether the token belongs to another repository than the one of the upload.</param>
    /// <exception cref="ForbiddenException"><paramref name="otherRepository" /> is <see langword="true" />.</exception>
    public static void ThrowIfUploadTokenNotForRepository([DoesNotReturnIf(true)] bool otherRepository)
    {
      if (otherRepository)
      {
        throw new ForbiddenException(UploadErrors.TokenNotForRepository, "The upload token belongs to another repository.");
      }
    }
  }
  #endregion
}
