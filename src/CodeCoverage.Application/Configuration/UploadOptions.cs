// ------------------------------------------------------------------------------------------------
// <copyright file="UploadOptions.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Settings;

namespace CodeCoverage.Application.Configuration;

/// <summary>The <c>Uploads</c> settings group: the upload limits and authentication.</summary>
/// <remarks>Read through <c>IOptionsMonitor&lt;T&gt;</c>: a change in the settings store applies without a restart.</remarks>
public sealed class UploadOptions
{
  #region Fields

  /// <summary>The name of the configuration section.</summary>
  public const string SectionName = SettingDefinitions.UploadsGroup;

  #endregion

  #region Properties

  /// <summary>Gets or sets the largest accepted upload request, in megabytes.</summary>
  /// <value>200 by default.</value>
  public int MaxRequestMegabytes { get; set; } = 200;

  /// <summary>Gets or sets the largest decompressed size of an upload, in megabytes.</summary>
  /// <value>2048 by default.</value>
  public int MaxDecompressedMegabytes { get; set; } = 2048;

  /// <summary>Gets or sets the largest number of report files in one upload.</summary>
  /// <value>50 by default.</value>
  public int MaxFilesPerUpload { get; set; } = 50;

  /// <summary>Gets or sets the number of uploads accepted per minute, by token and by repository.</summary>
  /// <value>120 by default.</value>
  public int RateLimitPerMinute { get; set; } = 120;

  /// <summary>Gets or sets the audience expected in a GitHub Actions OIDC token.</summary>
  /// <value><see langword="null" /> by default: the public URL of the Api is expected.</value>
  public string? OidcAudience { get; set; }

  #endregion
}
