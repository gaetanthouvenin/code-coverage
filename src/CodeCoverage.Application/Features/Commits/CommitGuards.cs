// ------------------------------------------------------------------------------------------------
// <copyright file="CommitGuards.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Diagnostics.CodeAnalysis;

using CodeCoverage.Application.Exceptions;
using CodeCoverage.Domain.Commits;

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
  }

  extension(UnprocessableEntityException)
  {
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
