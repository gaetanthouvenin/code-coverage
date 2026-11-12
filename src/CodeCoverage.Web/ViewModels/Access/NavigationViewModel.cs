// ------------------------------------------------------------------------------------------------
// <copyright file="NavigationViewModel.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Web.ViewModels.Access;

/// <summary>What the navigation of a signed-in user shows: its entries depend on what it administers.</summary>
/// <param name="Login">The login of the user.</param>
/// <param name="IsInstanceAdmin">Whether the user administers the instance.</param>
/// <param name="Organizations">The active organizations the user takes part in, by login.</param>
/// <remarks>The flags only hide entries: every page and every message keeps checking the access itself.</remarks>
public sealed record NavigationViewModel(string Login, bool IsInstanceAdmin, IReadOnlyList<NavigationOrganizationViewModel> Organizations)
{
  #region Properties

  /// <summary>Gets a value indicating whether the user administers one organization at least.</summary>
  public bool IsOrganizationAdmin => Organizations.Any(static o => o.IsAdmin);

  #endregion
}
