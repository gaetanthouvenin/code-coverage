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
/// <remarks>The order of the validated mockup: the repositories, the upload tokens, then the settings.</remarks>
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

    Items =
    [
      new CcNavItem($"{root}/repositories", L["Tab_OrgRepositories"], CcIconName.Repo),
      new CcNavItem($"{root}/upload-tokens", L["Tab_UploadTokens"], CcIconName.Key),
      new CcNavItem($"{root}/settings", L["Tab_OrgSettings"], CcIconName.Gear),
    ];
  }

  #endregion
}
