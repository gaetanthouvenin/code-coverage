// ------------------------------------------------------------------------------------------------
// <copyright file="NotUploadedBehavior.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Domain.Config;

/// <summary>
///   What a status does when none of its flags was uploaded on the commit
///   (<c>flag_coverage_not_uploaded_behavior</c>).
/// </summary>
public enum NotUploadedBehavior
{
  /// <summary>The status is computed as usual.</summary>
  Include,

  /// <summary>No status is sent.</summary>
  Exclude,

  /// <summary>The status is computed and always passes.</summary>
  Pass,
}
