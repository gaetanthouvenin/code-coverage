// ------------------------------------------------------------------------------------------------
// <copyright file="NotFound.razor.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;

using CodeCoverage.Web.Resources;

namespace CodeCoverage.Web.Components.Pages;

/// <summary>The page shown for an address that matches no page.</summary>
/// <remarks>
///   The router renders it for an unknown route or a <c>NavigationManager.NotFound()</c>, and the
///   status code pages re-execute it for any other 404 answered without a body.
/// </remarks>
public partial class NotFound
{
  #region Injected services

  [Inject]
  private IStringLocalizer<Common> L { get; set; } = null!;

  #endregion
}
