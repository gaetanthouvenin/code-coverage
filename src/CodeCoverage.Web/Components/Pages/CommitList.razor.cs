// ------------------------------------------------------------------------------------------------
// <copyright file="CommitList.razor.cs" company="Gaëtan Thouvenin">
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
using CodeCoverage.Web.ViewModels.Commits;

namespace CodeCoverage.Web.Components.Pages;

/// <summary>
///   The commits of a branch of a repository, 25 a page: message, author, date, coverage, change against the parent on
///   the branch, state and sessions. The branch and the page are in the address (<c>?branch=</c>, <c>?page=</c>).
/// </summary>
public partial class CommitList
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

  /// <summary>Gets or sets the branch listed, or <see langword="null" /> for the default one.</summary>
  [SupplyParameterFromQuery(Name = "branch")]
  public string? Branch { get; set; }

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
  private TimeProvider Time { get; set; } = null!;

  [Inject]
  private IStringLocalizer<Common> L { get; set; } = null!;

  #endregion

  #region Properties

  private PageState State { get; set; } = PageState.Loading;

  private CommitPageViewModel? Commits { get; set; }

  private IReadOnlyList<CcSelectOption> BranchOptions { get; set; } = [];

  private IReadOnlyList<CcBreadcrumbItem> Crumbs { get; set; } = [];

  #endregion

  #region Lifecycle

  /// <inheritdoc />
  protected override Task OnParametersSetAsync()
  {
    Crumbs = [new CcBreadcrumbItem(L["Tab_Commits"])];

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

  private string CommitHref(string sha) => $"{Uri.EscapeDataString(Forge)}/{Uri.EscapeDataString(Owner)}/{Uri.EscapeDataString(Name)}/commit/{sha}";

  private string AgeOf(DateTimeOffset at) => DisplayFormatter.Age(at, Time.GetUtcNow(), L);

  private async Task LoadAsync()
  {
    State = PageState.Loading;
    var (forge, owner, name, branch, page) = (Forge, Owner, Name, Branch, Page ?? 1);
    var ticket = _latest.Begin();
    var result = await Runner.RunAsync((sender, ct) => sender.GetCommitPageViewModelAsync(forge, owner, name, branch, page, ct)).ConfigureAwait(true);

    // Another branch or page was asked since: its own load shows it.
    if (!_latest.IsLatest(ticket))
    {
      return;
    }

    // A page past the last one (an old link, a list that shrank) goes to the last real page: the page stays loading,
    // so the empty list of that page is never shown, and the branches of that page are left alone.
    if (result is { Succeeded: true, Value: { Rows.Count: 0, TotalCount: > 0 } past } && past.Page > CcPager.LastPage(past.TotalCount, past.PageSize))
    {
      Navigation.NavigateTo(Navigation.GetUriWithQueryParameter("page", CcPager.LastPage(past.TotalCount, past.PageSize)), replace: true);

      return;
    }

    (State, Commits) = result switch
    {
      { Succeeded: true, Value: { } commits } => (PageState.Ready, commits),
      { Succeeded: true } or { NotFound: true } => (PageState.Missing, null),
      var _ => (PageState.Failed, Commits),
    };
    BranchOptions = Commits is null ? [] : [.. Commits.Branches.Select(static b => new CcSelectOption(b, b))];
  }

  // The branch and the page go to the address: a reload or a shared link shows the same list.
  private void ChangeBranch(string? branch) =>
    Navigation.NavigateTo(Navigation.GetUriWithQueryParameters(new Dictionary<string, object?> { ["branch"] = branch, ["page"] = null }));

  private void ChangePage(int page) => Navigation.NavigateTo(Navigation.GetUriWithQueryParameter("page", page));

  #endregion
}
