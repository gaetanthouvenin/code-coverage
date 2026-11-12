// ------------------------------------------------------------------------------------------------
// <copyright file="AdminAudit.razor.cs" company="Gaëtan Thouvenin">
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
using CodeCoverage.Web.ViewModels.Operations;

namespace CodeCoverage.Web.Components.Pages;

/// <summary>
///   The audit log of the instance, reserved to its administrators: the entries recorded outside any organization only,
///   metadata only (date, user, action, target), filtered by family and searched, 25 a page.
/// </summary>
public partial class AdminAudit
{
  #region Fields

  // The search waits for the typing to pause: one request for a word, not one per key.
  private static readonly TimeSpan SearchDelay = TimeSpan.FromMilliseconds(300);

  private readonly LatestRequest _latest = new();

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

  private AuditPageViewModel? Entries { get; set; }

  private string Family { get; set; } = OperationsMapper.AllFamilies;

  private string? Search { get; set; }

  private int Page { get; set; } = 1;

  private IReadOnlyList<CcSelectOption> FamilyOptions { get; set; } = [];

  #endregion

  #region Lifecycle

  /// <inheritdoc />
  protected override Task OnInitializedAsync()
  {
    FamilyOptions =
    [
      new CcSelectOption(OperationsMapper.AllFamilies, L["AuditFamily_All"]),
      .. OperationsMapper.Families.Select(f => new CcSelectOption(f, L[$"AuditFamily_{f}"])),
    ];

    return LoadAsync();
  }

  #endregion

  #region Methods

  private static CcTone ToneOf(string? family) => family switch
  {
    "Secrets" => CcTone.Warning,
    "Organizations" => CcTone.Success,
    "Settings" or "Tokens" => CcTone.Info,
    var _ => CcTone.Neutral,
  };

  private async Task LoadAsync()
  {
    State = PageState.Loading;
    var (family, search, page) = (Family, Search, Page);
    var ticket = _latest.Begin();
    var result = await Runner.RunAsync((sender, ct) => sender.GetAuditPageViewModelAsync(family, search, page, ct)).ConfigureAwait(true);

    // Another family, search or page was asked since: its own load shows it.
    if (!_latest.IsLatest(ticket))
    {
      return;
    }

    (State, Entries) = result switch
    {
      { Succeeded: true } => (PageState.Ready, result.Value),
      { Forbidden: true } => (PageState.Denied, null),
      var _ => (PageState.Failed, Entries),
    };

    // A page past the last one (the log shrank since) goes to the last real page, in place of an empty list.
    if (Entries is { Entries.Count: 0, TotalCount: > 0 } shown && shown.Page > CcPager.LastPage(shown.TotalCount, shown.PageSize))
    {
      Page = CcPager.LastPage(shown.TotalCount, shown.PageSize);
      await LoadAsync().ConfigureAwait(true);
    }
  }

  private Task ChangeFamilyAsync(string? family)
  {
    (Family, Page) = (family ?? OperationsMapper.AllFamilies, 1);

    return LoadAsync();
  }

  private async Task ChangeSearchAsync(string? search)
  {
    (Search, Page) = (search, 1);
    var ticket = _latest.Begin();

    await Task.Delay(SearchDelay, Time).ConfigureAwait(true);

    // Typed again, or another load started, during the pause: that one reads the list.
    if (_latest.IsLatest(ticket))
    {
      await LoadAsync().ConfigureAwait(true);
    }
  }

  private Task ChangePageAsync(int page)
  {
    Page = page;

    return LoadAsync();
  }

  #endregion
}
