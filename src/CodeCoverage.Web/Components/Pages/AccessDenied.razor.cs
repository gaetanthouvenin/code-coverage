// ------------------------------------------------------------------------------------------------
// <copyright file="AccessDenied.razor.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;

using CodeCoverage.Web.Configuration.Authentication;
using CodeCoverage.Web.Resources;

namespace CodeCoverage.Web.Components.Pages;

/// <summary>The page shown to a signed-in person who lacks the permission a page requires.</summary>
/// <remarks>
///   A sign-in refused because the account reaches no repository lands here with
///   <c>?reason=</c><see cref="AccessDeniedReasons.NoRepository" />, and no session.
/// </remarks>
public partial class AccessDenied
{
  #region Parameters

  /// <summary>Gets or sets why the access was denied, one of <see cref="AccessDeniedReasons" />, or <see langword="null" />.</summary>
  [SupplyParameterFromQuery(Name = "reason")]
  public string? Reason { get; set; }

  #endregion

  #region Injected services

  [Inject]
  private IStringLocalizer<Common> L { get; set; } = null!;

  #endregion

  #region Properties

  /// <summary>Gets the explanation shown, which follows the reason.</summary>
  private string Description =>
    Reason == AccessDeniedReasons.NoRepository ? L["Access.NoRepository"] : L["AccessDenied_Description"];

  #endregion
}
