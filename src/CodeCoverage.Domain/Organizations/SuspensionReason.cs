// ------------------------------------------------------------------------------------------------
// <copyright file="SuspensionReason.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Domain.Organizations;

/// <summary>Why an organization is suspended, stored as text.</summary>
public enum SuspensionReason
{
  /// <summary>An instance administrator suspended the organization.</summary>
  Administrator,

  /// <summary>The account uninstalled or suspended the application on its forge.</summary>
  AppUninstalled,

  /// <summary>The account was renamed to a login that is a segment of a route of the product.</summary>
  ReservedLogin,
}
