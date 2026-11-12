// ------------------------------------------------------------------------------------------------
// <copyright file="Home.razor.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Globalization;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Localization;

using CodeCoverage.Web.Components.Layout;
using CodeCoverage.Web.Components.Ui.Models;
using CodeCoverage.Web.Mapping;
using CodeCoverage.Web.Messaging;
using CodeCoverage.Web.Resources;
using CodeCoverage.Web.ViewModels.Access;
using CodeCoverage.Web.ViewModels.Repositories;

namespace CodeCoverage.Web.Components.Pages;

/// <summary>
///   The repositories the user can read, one panel per organization of the user (or the one <c>?org=</c> names): coverage
///   of the default branch, 30-day trend, last covered commit and flags, with a search and an active / inactive filter.
///   One request per organization; an organization the user does not belong to shows the same empty state as none.
/// </summary>
public partial class Home
{
  #region Fields

  private const string AllStates = "all";

  private const string ActiveState = "active";

  private const string InactiveState = "inactive";

  private readonly LatestRequest _latest = new();

  #endregion

  #region Parameters

  /// <summary>Gets or sets the organization the list is narrowed to, as <c>{forge}/{login}</c>, or <see langword="null" /> for all.</summary>
  [SupplyParameterFromQuery(Name = NavMenu.OrganizationParameter)]
  public string? Organization { get; set; }

  /// <summary>Gets or sets the state of the authentication, cascaded by the router.</summary>
  [CascadingParameter]
  public Task<AuthenticationState>? AuthenticationState { get; set; }

  #endregion

  #region Injected services

  [Inject]
  private UiRequestRunner Runner { get; set; } = null!;

  [Inject]
  private TimeProvider Time { get; set; } = null!;

  [Inject]
  private IStringLocalizer<Common> L { get; set; } = null!;

  #endregion

  #region Properties

  private PageState State { get; set; } = PageState.Loading;

  private IReadOnlyList<HomeOrganizationViewModel>? Organizations { get; set; }

  private bool IsInstanceAdminWithoutOrganization { get; set; }

  private string? Filter { get; set; }

  private string StateFilter { get; set; } = AllStates;

  private IReadOnlyList<CcTabItem> StateTabs { get; set; } = [];

  private bool IsFiltered => !string.IsNullOrWhiteSpace(Filter) || StateFilter is not AllStates;

  // The search keeps the names that contain it, whatever its case; the filter keeps the active or the disabled ones.
  // Unfiltered, an organization without readable repository keeps its panel, with its empty body.
  private IEnumerable<(HomeOrganizationViewModel Organization, IReadOnlyList<RepositorySummaryViewModel> Repositories)> VisibleOrganizations =>
    (Organizations ?? []).Select(o => (o, (IReadOnlyList<RepositorySummaryViewModel>)[.. o.Repositories.Where(IsVisible)]))
                         .Where(g => g.Item2.Count > 0 || !IsFiltered);

  #endregion

  #region Lifecycle

  /// <inheritdoc />
  protected override Task OnParametersSetAsync()
  {
    StateTabs =
    [
      new CcTabItem(AllStates, L["Filter_All"]),
      new CcTabItem(ActiveState, L["Filter_Active"]),
      new CcTabItem(InactiveState, L["Filter_Inactive"]),
    ];

    return LoadAsync();
  }

  #endregion

  #region Methods

  private static string HrefOf(RepositorySummaryViewModel repository) =>
    $"{Uri.EscapeDataString(repository.Forge)}/{Uri.EscapeDataString(repository.Owner)}/{Uri.EscapeDataString(repository.Name)}";

  // The bar of the home page knows the coverage only: hits and misses in percent, no partial.
  private static double CoverageOf(RepositorySummaryViewModel repository) => (double)(repository.CoverageValue ?? 0);

  private static double MissesOf(RepositorySummaryViewModel repository) => repository.CoverageValue is { } value ? (double)(100 - value) : 0;

  private static IReadOnlyList<double?> TrendOf(RepositorySummaryViewModel repository) => [.. repository.Trend.Select(static c => (double?)c)];

  private bool IsVisible(RepositorySummaryViewModel repository) =>
    (string.IsNullOrWhiteSpace(Filter) || repository.Name.Contains(Filter.Trim(), StringComparison.OrdinalIgnoreCase))
    && StateFilter switch
    {
      ActiveState => !repository.IsInactive,
      InactiveState => repository.IsInactive,
      var _ => true,
    };

  // The text alternative of the trend says where it starts and where it ends.
  private string TrendLabel(RepositorySummaryViewModel repository)
  {
    var known = repository.Trend.OfType<decimal>().ToList();

    return known.Count == 0
      ? L["Home_TrendNone"]
      : L["Home_TrendLabel", known[0].ToString("0.00", CultureInfo.CurrentCulture), known[^1].ToString("0.00", CultureInfo.CurrentCulture)];
  }

  private void ChangeState(string state) => StateFilter = state;

  private void ClearFilters() => (Filter, StateFilter) = (null, AllStates);

  private async Task LoadAsync()
  {
    State = PageState.Loading;
    var ticket = _latest.Begin();

    // Signed out (the page still renders its shell), nothing is read: the sign-in takes over.
    if (AuthenticationState is not null && (await AuthenticationState.ConfigureAwait(true)).User.Identity is not { IsAuthenticated: true })
    {
      (State, Organizations) = (PageState.Ready, []);
      return;
    }

    var navigation = await Runner.RunAsync(static (sender, ct) => sender.GetNavigationViewModelAsync(ct)).ConfigureAwait(true);

    // Another organization was asked since: its own load shows it.
    if (!_latest.IsLatest(ticket))
    {
      return;
    }

    if (!navigation.Succeeded)
    {
      State = PageState.Failed;
      return;
    }

    IsInstanceAdminWithoutOrganization = navigation.Value.IsInstanceAdmin && navigation.Value.Organizations.Count == 0;

    // ?org= is matched against the organizations of the user only: another value narrows to nothing, as no organization.
    IEnumerable<NavigationOrganizationViewModel> wanted = string.IsNullOrEmpty(Organization)
      ? navigation.Value.Organizations
      : navigation.Value.Organizations.Where(o => string.Equals(o.Key, Organization, StringComparison.Ordinal));
    var organizations = new List<HomeOrganizationViewModel>();

    foreach (var organization in wanted)
    {
      var (forge, login) = (organization.Forge, organization.Login);
      var result = await Runner.RunAsync((sender, ct) => sender.ListRepositorySummaryViewModelsAsync(forge, login, ct)).ConfigureAwait(true);

      if (!_latest.IsLatest(ticket))
      {
        return;
      }

      if (!result.Succeeded)
      {
        State = PageState.Failed;
        return;
      }

      organizations.Add(new HomeOrganizationViewModel(organization.Login, organization.ForgeName, result.Value));
    }

    (State, Organizations) = (PageState.Ready, organizations);
  }

  #endregion
}
