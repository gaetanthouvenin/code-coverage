// ------------------------------------------------------------------------------------------------
// <copyright file="AdminTabs.razor.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;

using CodeCoverage.Web.Components.Ui.Icons;
using CodeCoverage.Web.Components.Ui.Models;
using CodeCoverage.Web.Resources;

namespace CodeCoverage.Web.Components.Admin;

/// <summary>The sections of the instance, in pill tabs: organizations, settings, operations and audit log.</summary>
/// <remarks>The pages of the operations and of the audit log come with M3.40 and M3.41: their tabs are shown already.</remarks>
public partial class AdminTabs
{
  #region Injected services

  [Inject]
  private IStringLocalizer<Common> L { get; set; } = null!;

  #endregion

  #region Properties

  private IReadOnlyList<CcNavItem> Items { get; set; } = [];

  #endregion

  #region Lifecycle

  /// <inheritdoc />
  protected override void OnInitialized() =>
    Items =
    [
      new CcNavItem("admin/organizations", L["Tab_Organizations"], CcIconName.Layers),
      new CcNavItem("admin/settings", L["Tab_InstanceSettings"], CcIconName.Gear),
      new CcNavItem("admin/operations", L["Tab_Operations"], CcIconName.Activity),
      new CcNavItem("admin/audit", L["Tab_Audit"], CcIconName.List),
    ];

  #endregion
}
