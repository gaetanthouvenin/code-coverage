// ------------------------------------------------------------------------------------------------
// <copyright file="OrganizationTabs.razor.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;

using CodeCoverage.Web.Components.Ui.Icons;
using CodeCoverage.Web.Components.Ui.Models;
using CodeCoverage.Web.Resources;

namespace CodeCoverage.Web.Components.Organizations;

/// <summary>The sections of an organization, in pill tabs, for its administrators.</summary>
/// <remarks>
///   Only the sections whose page exists are shown: the settings now, the repositories (M3.39) and the upload tokens
///   (M3.38) when they land.
/// </remarks>
public partial class OrganizationTabs
{
  #region Parameters

  /// <summary>Gets or sets the forge segment of the organization.</summary>
  [Parameter, EditorRequired]
  public string Forge { get; set; } = null!;

  /// <summary>Gets or sets the login of the organization.</summary>
  [Parameter, EditorRequired]
  public string Login { get; set; } = null!;

  #endregion

  #region Injected services

  [Inject]
  private IStringLocalizer<Common> L { get; set; } = null!;

  #endregion

  #region Properties

  private IReadOnlyList<CcNavItem> Items { get; set; } = [];

  #endregion

  #region Lifecycle

  /// <inheritdoc />
  protected override void OnParametersSet()
  {
    var root = $"orgs/{Uri.EscapeDataString(Forge)}/{Uri.EscapeDataString(Login)}";

    Items = [new CcNavItem($"{root}/settings", L["Tab_OrgSettings"], CcIconName.Gear)];
  }

  #endregion
}
