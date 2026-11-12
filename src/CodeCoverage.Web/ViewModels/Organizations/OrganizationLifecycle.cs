// ------------------------------------------------------------------------------------------------
// <copyright file="OrganizationLifecycle.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Web.ViewModels.Organizations;

/// <summary>Where an organization stands in its lifecycle.</summary>
public enum OrganizationLifecycle
{
  /// <summary>Its account installed the GitHub App and waits for its admission.</summary>
  Pending,

  /// <summary>Admitted: its members use the console.</summary>
  Active,

  /// <summary>Suspended: its members see « not found », its data is kept.</summary>
  Suspended,
}
