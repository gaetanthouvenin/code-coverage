// ------------------------------------------------------------------------------------------------
// <copyright file="OrganizationState.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Domain.Organizations;

/// <summary>The place of an organization in its lifecycle, stored as text.</summary>
public enum OrganizationState
{
  /// <summary>The account connected the forge and waits for an instance administrator; nothing is tracked.</summary>
  Pending,

  /// <summary>The organization was admitted: a scope can enter it.</summary>
  Active,

  /// <summary>The organization is kept but no scope can enter it.</summary>
  Suspended,
}
