// ------------------------------------------------------------------------------------------------
// <copyright file="NavMenu.razor.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.Extensions.Localization;

using CodeCoverage.Web.Components.Ui.Models;
using CodeCoverage.Web.Mapping;
using CodeCoverage.Web.Resources;
using CodeCoverage.Web.ViewModels.Access;

namespace CodeCoverage.Web.Components.Layout;

/// <summary>
///   Builds the top bar of the validated mockup: its entries (Repositories; Organization for the administrators of an
///   organization; Instance for the administrators of the instance; My account), the switcher of organization and the
///   user menu.
/// </summary>
/// <remarks>
///   The entries only hide what the user cannot use: every page keeps checking the access itself. The entries of pages
///   that do not exist yet are rendered all the same.
/// </remarks>
public partial class NavMenu : IDisposable
{
  #region Fields

  /// <summary>The query parameter that filters the home page on one organization: <c>?org={forge}/{login}</c>.</summary>
  public const string OrganizationParameter = "org";

  // The first segments that never start the address of a repository.
  private static readonly HashSet<string> ReservedSegments =
    new(["orgs", "admin", "account", "setup", "signin", "signout", "error", "not-found", "access-denied", "ui-gallery", "health", "_content", "_framework"],
        StringComparer.OrdinalIgnoreCase);

  #endregion

  #region Parameters

  /// <summary>Gets or sets the navigation of the signed-in user, or <see langword="null" /> when nobody is signed in.</summary>
  [Parameter]
  public NavigationViewModel? Model { get; set; }

  #endregion

  #region Injected services

  [Inject]
  private NavigationManager Navigation { get; set; } = null!;

  [Inject]
  private IStringLocalizer<Common> L { get; set; } = null!;

  #endregion

  #region Properties

  private IReadOnlyList<CcNavItem> Items { get; set; } = [];

  private IReadOnlyList<CcSelectOption> Switcher { get; set; } = [];

  private NavigationOrganizationViewModel? CurrentOrganization { get; set; }

  // With no current organization and no « All organizations » choice, a neutral choice is selected: the browser would
  // otherwise show the first organization as if it were the current one.
  private string? SwitcherPlaceholder { get; set; }

  private string? RoleLabel { get; set; }

  #endregion

  #region Lifecycle

  /// <inheritdoc />
  protected override void OnInitialized() =>
    Navigation.LocationChanged += OnLocationChanged;

  /// <inheritdoc />
  protected override void OnParametersSet() => Build();

  #endregion

  #region Methods

  /// <summary>Reads the organization an address names: a repository, a page of an organization, or the filter of the home page.</summary>
  /// <param name="relativePath">The address, relative to the base of the site.</param>
  /// <returns>The forge segment and the login, or <see langword="null" />.</returns>
  internal static (string Forge, string Login)? OrganizationOf(string relativePath)
  {
    ArgumentNullException.ThrowIfNull(relativePath);

    var queryStart = relativePath.IndexOf('?', StringComparison.Ordinal);
    var path = (queryStart < 0 ? relativePath : relativePath[..queryStart]).Trim('/');
    var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);

    if (segments.Length >= 3 && string.Equals(segments[0], "orgs", StringComparison.OrdinalIgnoreCase))
    {
      return AccessMapper.ToForgeSegment(segments[1]) is { } organizationForge ? (organizationForge, Uri.UnescapeDataString(segments[2])) : null;
    }

    if (segments.Length >= 2 && IsRepositoryAddress(segments))
    {
      // The owner of an Azure DevOps or GitLab repository is written organization:project.
      var owner = Uri.UnescapeDataString(segments[1]);
      var colon = owner.IndexOf(':', StringComparison.Ordinal);

      // An alias of ADR-014 (gh, gl, bb) names the forge the organizations carry.
      return (AccessMapper.ToForgeSegment(segments[0])!, colon < 0 ? owner : owner[..colon]);
    }

    if (segments.Length == 0 && queryStart >= 0)
    {
      foreach (var pair in relativePath[(queryStart + 1)..].Split('&', StringSplitOptions.RemoveEmptyEntries))
      {
        var equals = pair.IndexOf('=', StringComparison.Ordinal);

        if (equals > 0
            && pair[..equals] == OrganizationParameter
            && Uri.UnescapeDataString(pair[(equals + 1)..]).Split('/') is [var forge, var login]
            && AccessMapper.ToForgeSegment(forge) is { } filterForge)
        {
          return (filterForge, login);
        }
      }
    }

    return null;
  }

  private static bool IsRepositoryAddress(string[] segments) =>
    !ReservedSegments.Contains(segments[0]) && AccessMapper.ToForgeSegment(segments[0]) is not null;

  private void Build()
  {
    var relative = Navigation.ToBaseRelativePath(Navigation.Uri);
    var path = relative.Split('?', '#')[0].Trim('/');
    var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
    var section = segments.Length == 0 ? string.Empty : segments[0].ToLowerInvariant();
    var organizations = Model?.Organizations ?? [];
    var addressed = OrganizationOf(relative);

    CurrentOrganization = addressed is { } named
                            ? organizations.FirstOrDefault(o => string.Equals(o.Forge, named.Forge, StringComparison.OrdinalIgnoreCase)
                                                                && string.Equals(o.Login, named.Login, StringComparison.OrdinalIgnoreCase))
                            : null;

    var items = new List<CcNavItem> { new(string.Empty, L["Nav_Repositories"], Active: segments.Length == 0 || IsRepositoryAddress(segments)) };

    if (Model is not null)
    {
      if (Model.IsOrganizationAdmin)
      {
        var target = CurrentOrganization is { IsAdmin: true } ? CurrentOrganization : organizations.First(static o => o.IsAdmin);

        items.Add(new CcNavItem($"orgs/{target.Forge}/{Uri.EscapeDataString(target.Login)}/repositories", L["Nav_Organization"], Active: section == "orgs"));
      }

      if (Model.IsInstanceAdmin)
      {
        items.Add(new CcNavItem("admin/organizations", L["Nav_Instance"], Active: section == "admin"));
      }

      items.Add(new CcNavItem("account/tokens", L["Nav_Account"], Active: section == "account"));
    }

    Items = items;
    Switcher = SwitcherOptions(organizations, allowAll: section.Length == 0);
    SwitcherPlaceholder = CurrentOrganization is null && !Switcher.Any(static o => o.Value.Length == 0) ? L["Org_Choose"].Value : null;
    RoleLabel = Model is null ? null : RoleOf(Model);
  }

  private IReadOnlyList<CcSelectOption> SwitcherOptions(IReadOnlyList<NavigationOrganizationViewModel> organizations, bool allowAll)
  {
    if (organizations.Count == 0)
    {
      return [];
    }

    var options = new List<CcSelectOption>();

    if (allowAll && organizations.Count > 1)
    {
      options.Add(new CcSelectOption(string.Empty, L["Org_All"]));
    }

    foreach (var organization in organizations)
    {
      // The same login on two forges is told apart by the name of its forge.
      var duplicated = organizations.Count(o => string.Equals(o.Login, organization.Login, StringComparison.OrdinalIgnoreCase)) > 1;
      var label = duplicated ? $"{organization.Login} ({L[$"Forge_{organization.ForgeName}"]})" : organization.Login;

      options.Add(new CcSelectOption(organization.Key, label));
    }

    return options;
  }

  private string RoleOf(NavigationViewModel model)
  {
    if (model.IsInstanceAdmin)
    {
      return L["Role_InstanceAdmin"];
    }

    var admin = CurrentOrganization?.IsAdmin ?? model.IsOrganizationAdmin;

    return admin ? L["Role_OrganizationAdmin"] : L["Role_Participant"];
  }

  private void OnOrganizationChanged(string? key)
  {
    var section = Navigation.ToBaseRelativePath(Navigation.Uri).Split('?', '#')[0].Trim('/').Split('/')[0];
    var organization = Model?.Organizations.FirstOrDefault(o => o.Key == key);

    if (organization is null)
    {
      // The neutral choice leads nowhere; « All organizations » leads to the home page unfiltered.
      if (SwitcherPlaceholder is null)
      {
        Navigation.NavigateTo("./");
      }

      return;
    }

    // A page of an organization stays one: the same section of the organization chosen; anywhere else, the home page
    // filtered on it.
    Navigation.NavigateTo(
      string.Equals(section, "orgs", StringComparison.OrdinalIgnoreCase) && organization.IsAdmin
        ? $"orgs/{organization.Forge}/{Uri.EscapeDataString(organization.Login)}/repositories"
        : $"./?{OrganizationParameter}={Uri.EscapeDataString(organization.Key)}"
    );
  }

  private void OnLocationChanged(object? sender, LocationChangedEventArgs e) =>
    _ = InvokeAsync(() =>
    {
      Build();
      StateHasChanged();
    });

  #endregion

  #region Implementation of IDisposable

  /// <inheritdoc />
  public void Dispose()
  {
    Navigation.LocationChanged -= OnLocationChanged;
    GC.SuppressFinalize(this);
  }

  #endregion
}
