// ------------------------------------------------------------------------------------------------
// <copyright file="NavigationOrganizationViewModel.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Web.ViewModels.Access;

/// <summary>An organization the signed-in user takes part in, as the navigation shows it.</summary>
/// <param name="Forge">The forge segment of its address, e.g. <c>github</c>.</param>
/// <param name="ForgeName">
///   The name of its forge in the resources (<c>Forge_{name}</c>), e.g. <c>GitHub</c>, to tell apart two logins of different
///   forges.
/// </param>
/// <param name="Login">Its login on its forge.</param>
/// <param name="IsAdmin">Whether the user administers it.</param>
public sealed record NavigationOrganizationViewModel(string Forge, string ForgeName, string Login, bool IsAdmin)
{
  #region Properties

  /// <summary>Gets the value of the organization in the switcher: <c>{forge}/{login}</c>.</summary>
  public string Key => $"{Forge}/{Login}";

  #endregion
}
