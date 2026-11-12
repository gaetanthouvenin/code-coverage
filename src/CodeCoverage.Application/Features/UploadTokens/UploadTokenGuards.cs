// ------------------------------------------------------------------------------------------------
// <copyright file="UploadTokenGuards.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Diagnostics.CodeAnalysis;

using CodeCoverage.Application.Exceptions;
using CodeCoverage.Domain.Tokens;

namespace CodeCoverage.Application.Features.UploadTokens;

/// <summary>
///   The refusals of the upload tokens, in the form of
///   <see cref="ArgumentNullException.ThrowIfNull(object?, string?)" />: one place holds the code and the message of each.
/// </summary>
internal static class UploadTokenGuards
{
  #region Methods

  extension(ConflictException)
  {
    /// <summary>Throws when an imported value is already a token of the organization.</summary>
    /// <param name="added">Whether the token was added.</param>
    /// <exception cref="ConflictException"><paramref name="added" /> is <see langword="false" />.</exception>
    public static void ThrowIfUploadTokenAlreadyImported([DoesNotReturnIf(false)] bool added)
    {
      if (!added)
      {
        throw new ConflictException(UploadTokenErrors.AlreadyImported, "This value is already a token of the organization.");
      }
    }
  }

  extension(NotFoundException)
  {
    /// <summary>Throws when a token is missing or is not one of the given owner.</summary>
    /// <param name="token">The token that was looked for, or <see langword="null" />.</param>
    /// <param name="scope">The scope it must have.</param>
    /// <param name="repositoryId">The repository it must have, for that scope.</param>
    /// <exception cref="NotFoundException"><paramref name="token" /> is <see langword="null" /> or belongs to another owner.</exception>
    public static void ThrowIfUploadTokenNotFound([NotNull] UploadToken? token, UploadTokenScope scope, long? repositoryId)
    {
      if (token is null || token.Scope != scope || token.RepositoryId != repositoryId)
      {
        throw new NotFoundException(UploadTokenErrors.NotFound, "This token does not exist.");
      }
    }
  }

  #endregion
}
