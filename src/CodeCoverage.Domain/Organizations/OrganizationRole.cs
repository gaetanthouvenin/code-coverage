// ------------------------------------------------------------------------------------------------
// <copyright file="OrganizationRole.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Domain.Organizations;

/// <summary>The role a user holds in an organization.</summary>
public enum OrganizationRole
{
  /// <summary>The user can reach at least one repository of the organization, and has no right on the organization.</summary>
  Participant,

  /// <summary>The user administers the organization.</summary>
  Admin,
}
