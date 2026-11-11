// ------------------------------------------------------------------------------------------------
// <copyright file="GitHubAccountTypes.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Organizations;

namespace CodeCoverage.Domain.Contracts.GitHub;

/// <summary>Reads the kind of account GitHub names in its payloads.</summary>
public static class GitHubAccountTypes
{
  #region Methods

  /// <summary>Converts the kind of account GitHub names (<c>type</c> or <c>target_type</c>).</summary>
  /// <param name="type">The kind as GitHub spells it, possibly <see langword="null" />.</param>
  /// <returns>
  ///   <see cref="OrganizationAccountType.User" /> for <c>User</c>, whatever its case; otherwise
  ///   <see cref="OrganizationAccountType.Organization" />, which a missing kind is taken for.
  /// </returns>
  public static OrganizationAccountType ToAccountType(string? type) =>
    string.Equals(type, "User", StringComparison.OrdinalIgnoreCase) ? OrganizationAccountType.User : OrganizationAccountType.Organization;

  #endregion
}
