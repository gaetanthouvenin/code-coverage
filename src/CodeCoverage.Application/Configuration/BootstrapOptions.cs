// ------------------------------------------------------------------------------------------------
// <copyright file="BootstrapOptions.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Application.Configuration;

/// <summary>
///   The bootstrap values of the access, read from the environment only (<c>CodeCoverage__Bootstrap__AdminUserIds</c>):
///   never a setting of the catalog, never edited in the console.
/// </summary>
public sealed class BootstrapOptions
{
  #region Fields

  /// <summary>The name of the configuration section.</summary>
  public const string SectionName = "CodeCoverage:Bootstrap";

  #endregion

  #region Properties

  /// <summary>
  ///   Gets or sets the numeric GitHub user identifiers of the first administrators of the instance, separated by
  ///   commas, which add to <see cref="AccessOptions.AdminUserIds" />.
  /// </summary>
  /// <value><see langword="null" /> by default.</value>
  public string? AdminUserIds { get; set; }

  #endregion
}
