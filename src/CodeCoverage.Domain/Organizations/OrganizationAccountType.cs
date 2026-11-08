// ------------------------------------------------------------------------------------------------
// <copyright file="OrganizationAccountType.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Domain.Organizations;

/// <summary>The kind of forge account behind an organization, stored as text.</summary>
public enum OrganizationAccountType
{
  /// <summary>A shared account: an organization on GitHub or Azure DevOps, a workspace on Bitbucket.</summary>
  Organization,

  /// <summary>A personal account.</summary>
  User,
}
