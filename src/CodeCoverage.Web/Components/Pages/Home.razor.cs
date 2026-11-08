// ------------------------------------------------------------------------------------------------
// <copyright file="Home.razor.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;

using CodeCoverage.Web.Resources;

namespace CodeCoverage.Web.Components.Pages;

/// <summary>The home page, rendered in the shell.</summary>
/// <remarks>Empty for now: the list of the repositories fills it with M3.</remarks>
public partial class Home
{
  #region Injected services

  [Inject]
  private IStringLocalizer<Common> L { get; set; } = null!;

  #endregion
}
