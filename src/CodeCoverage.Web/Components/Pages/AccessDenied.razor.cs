// ------------------------------------------------------------------------------------------------
// <copyright file="AccessDenied.razor.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;

using CodeCoverage.Web.Resources;

namespace CodeCoverage.Web.Components.Pages;

/// <summary>The page shown to a signed-in person who lacks the permission a page requires.</summary>
/// <remarks>The authentication (M3) redirects here; until then the page is only reachable by its address.</remarks>
public partial class AccessDenied
{
  #region Injected services

  [Inject]
  private IStringLocalizer<Common> L { get; set; } = null!;

  #endregion
}
