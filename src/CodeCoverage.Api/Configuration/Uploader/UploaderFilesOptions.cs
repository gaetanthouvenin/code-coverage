// ------------------------------------------------------------------------------------------------
// <copyright file="UploaderFilesOptions.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Api.Configuration.Uploader;

/// <summary>Where the image keeps the standalone binaries of the uploader (ADR-012).</summary>
/// <remarks>
///   A value of the deployment, like the path of the log file: it comes from the environment
///   (<c>CodeCoverage__Uploader__Directory</c>), never from the settings of the database.
/// </remarks>
public sealed class UploaderFilesOptions
{
  #region Fields

  /// <summary>The configuration section of these options.</summary>
  public const string SectionName = "CodeCoverage:Uploader";

  /// <summary>The directory <c>Dockerfile.api</c> publishes the binaries to.</summary>
  public const string DefaultDirectory = "/app/uploader";

  #endregion

  #region Properties

  /// <summary>Gets or sets the directory that holds the binaries and their <c>.sha256</c> files.</summary>
  public string Directory { get; set; } = DefaultDirectory;

  #endregion
}
