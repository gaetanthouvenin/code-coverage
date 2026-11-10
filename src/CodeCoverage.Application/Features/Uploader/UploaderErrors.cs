// ------------------------------------------------------------------------------------------------
// <copyright file="UploaderErrors.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Application.Features.Uploader;

/// <summary>The error codes of the download of the standalone uploader.</summary>
public static class UploaderErrors
{
  #region Fields

  /// <summary>The runtime identifier is not one the uploader is published for.</summary>
  public const string UnknownRuntime = "Uploader.UnknownRuntime";

  /// <summary>The binary of this runtime is not shipped with this server.</summary>
  public const string NotAvailable = "Uploader.NotAvailable";

  #endregion
}
