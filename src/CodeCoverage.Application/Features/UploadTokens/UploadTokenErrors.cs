// ------------------------------------------------------------------------------------------------
// <copyright file="UploadTokenErrors.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Application.Features.UploadTokens;

/// <summary>The error codes of the upload tokens.</summary>
public static class UploadTokenErrors
{
  #region Fields

  /// <summary>The token does not exist in this organization or for this repository.</summary>
  public const string NotFound = "UploadToken.NotFound";

  /// <summary>The same value is already a token of this organization.</summary>
  public const string AlreadyImported = "UploadToken.AlreadyImported";

  /// <summary>The label is empty or too long.</summary>
  public const string InvalidLabel = "UploadToken.InvalidLabel";

  /// <summary>The imported value is not a Codecov token.</summary>
  public const string InvalidImportedValue = "UploadToken.InvalidImportedValue";

  /// <summary>The expiry is not in the future.</summary>
  public const string InvalidExpiry = "UploadToken.InvalidExpiry";

  #endregion
}
