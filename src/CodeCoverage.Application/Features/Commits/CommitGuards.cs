// ------------------------------------------------------------------------------------------------
// <copyright file="CommitGuards.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Diagnostics.CodeAnalysis;

using CodeCoverage.Application.Exceptions;
using CodeCoverage.Domain.Commits;
using CodeCoverage.Domain.Coverage;

namespace CodeCoverage.Application.Features.Commits;

/// <summary>
///   The refusals of the commits, in the form of
///   <see cref="ArgumentNullException.ThrowIfNull(object?, string?)" />: one place holds the code and the
///   message of each.
/// </summary>
internal static class CommitGuards
{
  #region Methods

  extension(NotFoundException)
  {
    /// <summary>Throws when a commit was not found in the organization of the scope.</summary>
    /// <param name="commit">The commit that was looked for.</param>
    /// <exception cref="NotFoundException"><paramref name="commit" /> is <see langword="null" />.</exception>
    public static void ThrowIfCommitNotFound([NotNull] Commit? commit)
    {
      if (commit is null)
      {
        throw new NotFoundException(CommitErrors.NotFound, "The commit does not exist.");
      }
    }

    /// <summary>Throws when the report of a commit has no such file.</summary>
    /// <param name="file">The file that was looked for.</param>
    /// <exception cref="NotFoundException"><paramref name="file" /> is <see langword="null" />.</exception>
    public static void ThrowIfCommitFileNotFound([NotNull] FileCoverage? file)
    {
      if (file is null)
      {
        throw new NotFoundException(CommitErrors.FileNotFound, "The report of the commit has no such file.");
      }
    }
  }

  extension(UnprocessableEntityException)
  {
    /// <summary>Throws when the reports of a commit are no longer stored: only its totals are kept.</summary>
    /// <param name="compacted">Whether the commit was compacted.</param>
    /// <exception cref="UnprocessableEntityException"><paramref name="compacted" /> is <see langword="true" />.</exception>
    public static void ThrowIfCommitCompacted([DoesNotReturnIf(true)] bool compacted)
    {
      if (compacted)
      {
        throw new UnprocessableEntityException(CommitErrors.Compacted, "The reports of the commit are no longer stored: only its totals are kept.");
      }
    }

    /// <summary>Throws when a commit has no upload to process again.</summary>
    /// <param name="uploads">The uploads of the commit.</param>
    /// <exception cref="ArgumentNullException"><paramref name="uploads" /> is <see langword="null" />.</exception>
    /// <exception cref="UnprocessableEntityException"><paramref name="uploads" /> is empty.</exception>
    public static void ThrowIfCommitNoUpload(IReadOnlyCollection<Upload> uploads)
    {
      ArgumentNullException.ThrowIfNull(uploads);

      if (uploads.Count == 0)
      {
        throw new UnprocessableEntityException(CommitErrors.NoUpload, "The commit has no upload to process again.");
      }
    }

    /// <summary>Throws when the raw files of an upload of a commit are no longer kept.</summary>
    /// <param name="expired">Whether one upload at least can no longer be parsed again.</param>
    /// <exception cref="UnprocessableEntityException"><paramref name="expired" /> is <see langword="true" />.</exception>
    public static void ThrowIfCommitRawExpired([DoesNotReturnIf(true)] bool expired)
    {
      if (expired)
      {
        throw new UnprocessableEntityException(CommitErrors.RawExpired, "The raw files of an upload of this commit are no longer kept.");
      }
    }
  }

  #endregion
}
