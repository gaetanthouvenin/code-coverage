// ------------------------------------------------------------------------------------------------
// <copyright file="PullRequestList.razor.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;

using CodeCoverage.Web.Components.Ui;
using CodeCoverage.Web.Components.Ui.Models;
using CodeCoverage.Web.Mapping;
using CodeCoverage.Web.Messaging;
using CodeCoverage.Web.Resources;
using CodeCoverage.Web.ViewModels.PullRequests;

namespace CodeCoverage.Web.Components.Pages;

/// <summary>
///   The pull requests of a repository, 25 a page and filtered by state: number, title, author, state, patch coverage and
///   change of the project coverage. The number links nowhere until the page of a pull request exists (M3.31).
/// </summary>
public partial class PullRequestList
{
  #region Fields

  private readonly LatestRequest _latest = new();

  #endregion

  #region Parameters

  /// <summary>Gets or sets the forge segment of the address.</summary>
  [Parameter]
  public string Forge { get; set; } = null!;

  /// <summary>Gets or sets the owner of the repository.</summary>
  [Parameter]
  public string Owner { get; set; } = null!;

  /// <summary>Gets or sets the name of the repository.</summary>
  [Parameter]
  public string Name { get; set; } = null!;

  /// <summary>Gets or sets the state listed: <c>open</c> (the default), <c>merged</c>, <c>closed</c> or <c>all</c>.</summary>
  [SupplyParameterFromQuery(Name = "state")]
  public string? StateFilter { get; set; }

  /// <summary>Gets or sets the page listed, from 1.</summary>
  [SupplyParameterFromQuery(Name = "page")]
  public int? Page { get; set; }

  #endregion

  #region Injected services

  [Inject]
  private UiRequestRunner Runner { get; set; } = null!;

  [Inject]
  private NavigationManager Navigation { get; set; } = null!;

  [Inject]
  private IStringLocalizer<Common> L { get; set; } = null!;

  #endregion

  #region Properties

  private PageState State { get; set; } = PageState.Loading;

  private PullRequestPageViewModel? Pulls { get; set; }

  private IReadOnlyList<CcTabItem> StateTabs { get; set; } = [];

  private IReadOnlyList<CcBreadcrumbItem> Crumbs { get; set; } = [];

  #endregion

  #region Lifecycle

  /// <inheritdoc />
  protected override Task OnParametersSetAsync()
  {
    Crumbs = [new CcBreadcrumbItem(L["Tab_Pulls"])];
    StateTabs =
    [
      new CcTabItem("open", L["Pull_Open"]),
      new CcTabItem("merged", L["Pull_Merged"]),
      new CcTabItem("closed", L["Pull_Closed"]),
      new CcTabItem("all", L["Filter_All"]),
    ];

    return LoadAsync();
  }

  #endregion

  #region Methods

  private static string DeltaClass(int trend) => trend switch
  {
    > 0 => "cc-delta--up",
    < 0 => "cc-delta--down",
    var _ => "cc-dim",
  };

  private async Task LoadAsync()
  {
    State = PageState.Loading;
    var (forge, owner, name, state, page) = (Forge, Owner, Name, StateFilter, Page ?? 1);
    var ticket = _latest.Begin();
    var result = await Runner.RunAsync((sender, ct) => sender.GetPullRequestPageViewModelAsync(forge, owner, name, state, page, ct)).ConfigureAwait(true);

    // Another state or page was asked since: its own load shows it.
    if (!_latest.IsLatest(ticket))
    {
      return;
    }

    // A page past the last one (an old link, a list that shrank) goes to the last real page: the page stays loading,
    // so the empty list of that page is never shown.
    if (result is { Succeeded: true, Value: { Rows.Count: 0, TotalCount: > 0 } past } && past.Page > CcPager.LastPage(past.TotalCount, past.PageSize))
    {
      Navigation.NavigateTo(Navigation.GetUriWithQueryParameter("page", CcPager.LastPage(past.TotalCount, past.PageSize)), replace: true);

      return;
    }

    (State, Pulls) = result switch
    {
      { Succeeded: true, Value: { } pulls } => (PageState.Ready, pulls),
      { Succeeded: true } or { NotFound: true } => (PageState.Missing, null),
      var _ => (PageState.Failed, Pulls),
    };
  }

  // The state and the page go to the address: a reload or a shared link shows the same list.
  private void ChangeState(string state) =>
    Navigation.NavigateTo(Navigation.GetUriWithQueryParameters(new Dictionary<string, object?> { ["state"] = state, ["page"] = null }));

  private void ChangePage(int page) => Navigation.NavigateTo(Navigation.GetUriWithQueryParameter("page", page));

  #endregion
}
