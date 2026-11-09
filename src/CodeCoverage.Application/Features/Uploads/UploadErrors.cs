// ------------------------------------------------------------------------------------------------
// <copyright file="UploadErrors.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Application.Features.Uploads;

/// <summary>The error codes of the uploads.</summary>
public static class UploadErrors
{
  #region Fields

  /// <summary>The credential is unknown, revoked or expired, or its owner is not an active organization.</summary>
  public const string TokenInvalid = "Upload.TokenInvalid";

  /// <summary>The token belongs to another repository.</summary>
  public const string TokenNotForRepository = "Upload.TokenNotForRepository";

  /// <summary>The OIDC token names another repository or another account.</summary>
  public const string OidcMismatch = "Upload.OidcMismatch";

  /// <summary>A file came before the text fields of the request.</summary>
  public const string MetadataMustPrecedeFiles = "Upload.MetadataMustPrecedeFiles";

  /// <summary>No file of the reports could be found in the repository.</summary>
  public const string NoFileMapped = "Upload.NoFileMapped";

  /// <summary>A report is in a format that is not supported.</summary>
  public const string UnsupportedFormat = "Upload.UnsupportedFormat";

  /// <summary>The upload named a synthetic merge commit and was moved to the head of the pull request.</summary>
  public const string MergeCommitRewritten = "Upload.MergeCommitRewritten";

  /// <summary>The content of the upload exceeds a limit.</summary>
  public const string TooLarge = "Upload.TooLarge";

  /// <summary>The repository is not written <c>owner/name</c>.</summary>
  public const string InvalidSlug = "Upload.InvalidSlug";

  #endregion
}
