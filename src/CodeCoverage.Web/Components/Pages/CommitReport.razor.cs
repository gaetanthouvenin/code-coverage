// ------------------------------------------------------------------------------------------------
// <copyright file="CommitReport.razor.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Globalization;

using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;

using CodeCoverage.Web.Components.Commits;
using CodeCoverage.Web.Components.Ui.Models;
using CodeCoverage.Web.Components.Ui.Services;
using CodeCoverage.Web.Mapping;
using CodeCoverage.Web.Messaging;
using CodeCoverage.Web.Resources;
using CodeCoverage.Web.ViewModels.Commits;

namespace CodeCoverage.Web.Components.Pages;

/// <summary>
///   The detailed report of a commit (ADR-016): the coverage of Codecov first, then the executed lines, the branches and
///   the methods of each file in one flat table with its total, sorted by a header and filtered by a search and the
///   list « Show ». The flag, the sort and the filter are in the address, so a shared address opens the same view.
/// </summary>
/// <remarks>
///   A commit processed before the detail was stored shows a banner and dashes everywhere but the coverage of Codecov;
///   a compacted one shows its state. A value that cannot be sorted, a dash, stays last in both directions.
/// </remarks>
public partial class CommitReport
{
  #region Fields

  /// <summary>The key of the sort by path.</summary>
  internal const string SortPath = "path";

  /// <summary>The key of the sort by coverage of Codecov, the default one.</summary>
  internal const string SortCodecov = "codecov";

  /// <summary>The key of the sort by executed lines.</summary>
  internal const string SortLines = "lines";

  /// <summary>The key of the sort by branches taken.</summary>
  internal const string SortBranches = "branches";

  /// <summary>The key of the sort by executed methods.</summary>
  internal const string SortMethods = "methods";

  /// <summary>The filter that keeps every file, the default one.</summary>
  internal const string FilterAll = "all";

  /// <summary>The filter that keeps the files below the target of the project status.</summary>
  internal const string FilterBelowTarget = "below-target";

  /// <summary>The filter that keeps the files with a branch not taken.</summary>
  internal const string FilterUncoveredBranches = "uncovered-branches";

  /// <summary>The filter that keeps the files with a method never executed.</summary>
  internal const string FilterUnexecutedMethods = "unexecuted-methods";

  private const string Descending = "desc";

  private static readonly string[] SortKeys = [SortCodecov, SortLines, SortBranches, SortMethods, SortPath];

  private readonly LatestRequest _latest = new();

  // The commit and flag the report was last read for: a change of sort or filter reads nothing again.
  private string? _loaded;

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

  /// <summary>Gets or sets the full SHA of the commit.</summary>
  [Parameter]
  public string Sha { get; set; } = null!;

  /// <summary>Gets or sets the flag the report is restricted to (<c>?flag=</c>).</summary>
  [SupplyParameterFromQuery(Name = "flag")]
  public string? Flag { get; set; }

  /// <summary>Gets or sets the sort (<c>?sort=branches:desc</c>); an unknown one is the default sort.</summary>
  [SupplyParameterFromQuery(Name = "sort")]
  public string? SortQuery { get; set; }

  /// <summary>Gets or sets the filter of the list « Show » (<c>?filter=</c>); an unknown one keeps every file.</summary>
  [SupplyParameterFromQuery(Name = "filter")]
  public string? FilterQuery { get; set; }

  #endregion

  #region Injected services

  [Inject]
  private UiRequestRunner Runner { get; set; } = null!;

  [Inject]
  private NavigationManager Navigation { get; set; } = null!;

  [Inject]
  private IToastService Toasts { get; set; } = null!;

  [Inject]
  private IStringLocalizer<Common> L { get; set; } = null!;

  #endregion

  #region Properties

  private static string[] GroupKeys { get; } = [SortLines, SortBranches, SortMethods];

  private PageState State { get; set; } = PageState.Loading;

  private CommitReportViewModel? Report { get; set; }

  private IReadOnlyList<CcBreadcrumbItem> Crumbs { get; set; } = [];

  private IReadOnlyList<CommitReportRowViewModel> Rows { get; set; } = [];

  private string? Search { get; set; }

  private string SortKey { get; set; } = SortCodecov;

  private bool IsDescending { get; set; }

  private string Filter { get; set; } = FilterAll;

  // The answer the header read once for the page: the reprocess action shows only on a true answer.
  private bool CanAdminister { get; set; }

  private bool Reprocessing { get; set; }

  private string Root => $"{Uri.EscapeDataString(Forge)}/{Uri.EscapeDataString(Owner)}/{Uri.EscapeDataString(Name)}";

  private string CommitHref => $"{Root}/commit/{Uri.EscapeDataString(Sha)}";

  private string LoadKey => $"{Forge}/{Owner}/{Name}/{Sha}#{Flag}";

  private string ShortSha => Sha.Length > DisplayFormatter.ShortShaLength ? Sha[..DisplayFormatter.ShortShaLength] : Sha;

  // « e4f5a6b on feature/webhook-retry · lines, branches and methods of each file ».
  private string Subtitle =>
    $"{(Report?.Branch is { } branch ? L["Commit_OnBranch", Report.ShortSha, branch].Value : Report?.ShortSha ?? ShortSha)} · {L["Report_Subtitle"]}";

  private string SortValue => $"{SortKey}:{(IsDescending ? Descending : "asc")}";

  private string FilterValue => Filter;

  private IReadOnlyList<CcSelectOption> FlagOptions =>
    [new CcSelectOption(string.Empty, L["Filter_AllFlags"]), .. (Report?.Flags ?? []).Select(static f => new CcSelectOption(f, f))];

  // « Below the target » needs a fixed target; the branches and methods filters need the detail.
  private IReadOnlyList<CcSelectOption> FilterOptions
  {
    get
    {
      var options = new List<CcSelectOption> { new(FilterAll, L["Report_ShowAll"]) };

      if (Report?.TargetText is { } target)
      {
        options.Add(new CcSelectOption(FilterBelowTarget, L["Report_ShowBelowTarget", target]));
      }

      if (Report is { HasDetail: true })
      {
        options.Add(new CcSelectOption(FilterUncoveredBranches, L["Report_ShowUncoveredBranches"]));
        options.Add(new CcSelectOption(FilterUnexecutedMethods, L["Report_ShowUnexecutedMethods"]));
      }

      return options;
    }
  }

  private IReadOnlyList<CcSelectOption> SortOptions =>
  [
    .. SortKeys.SelectMany(
      key => key is SortPath
        ? new[] { new CcSelectOption($"{key}:asc", L["Report_SortPathAsc"]), new CcSelectOption($"{key}:{Descending}", L["Report_SortPathDesc"]) }
        : [new CcSelectOption($"{key}:asc", L["Report_SortLowest", LabelOf(key)]), new CcSelectOption($"{key}:{Descending}", L["Report_SortHighest", LabelOf(key)])]),
  ];

  #endregion

  #region Lifecycle

  /// <inheritdoc />
  protected override async Task OnParametersSetAsync()
  {
    Crumbs = [new CcBreadcrumbItem(L["Tab_Commits"], $"{Root}/commits"), new CcBreadcrumbItem(ShortSha, CommitHref), new CcBreadcrumbItem(L["Report_Crumb"])];
    ReadSort(SortQuery);
    Filter = string.IsNullOrEmpty(FilterQuery) ? FilterAll : FilterQuery;

    if (string.Equals(_loaded, LoadKey, StringComparison.Ordinal))
    {
      BuildRows();
      return;
    }

    await LoadAsync().ConfigureAwait(true);
  }

  #endregion

  #region Methods

  private string LabelOf(string key) => key switch
  {
    SortLines => L["Report_Lines"],
    SortBranches => L["Report_Branches"],
    SortMethods => L["Report_Methods"],
    var _ => L["Report_Codecov"],
  };

  private string SourceHref(string path) => FileTree.SourceHref(Root, Sha, path);

  private string CardValue(CoverageCountViewModel? count) => count?.Text ?? DisplayFormatter.Dash;

  private string CardFoot(CoverageCountViewModel? count, string format) =>
    count is { } known
      ? L[format, known.Covered.ToString("N0", CultureInfo.CurrentCulture), known.Total.ToString("N0", CultureInfo.CurrentCulture)]
      : Report is { HasDetail: true }
        ? L["Report_NoMethods"]
        : L["Report_ReprocessFoot"];

  private string AriaSort(string key) =>
    !string.Equals(key, SortKey, StringComparison.Ordinal) ? "none" : IsDescending ? "descending" : "ascending";

  private string SortClass(string key) =>
    string.Equals(key, SortKey, StringComparison.Ordinal) ? "cc-report__sort cc-report__sort--on" : "cc-report__sort";

  private void PermissionKnown(bool canAdminister) => CanAdminister = canAdminister;

  private async Task LoadAsync()
  {
    var (forge, owner, name, sha, flag) = (Forge, Owner, Name, Sha, Flag);
    _loaded = LoadKey;
    var ticket = _latest.Begin();
    State = PageState.Loading;
    var result = await Runner.RunAsync((sender, ct) => sender.GetCommitReportViewModelAsync(forge, owner, name, sha, flag, ct)).ConfigureAwait(true);

    // Another flag or commit was chosen since: its own answer shows it.
    if (!_latest.IsLatest(ticket))
    {
      return;
    }

    (State, Report) = result switch
    {
      { Succeeded: true, Value: { } report } => (PageState.Ready, report),
      { Succeeded: true } or { NotFound: true } => (PageState.Missing, null),
      var _ => (PageState.Failed, Report),
    };
    BuildRows();
  }

  private void ReadSort(string? value)
  {
    var parts = (value ?? string.Empty).Split(':', 2);
    var key = SortKeys.FirstOrDefault(k => string.Equals(k, parts[0], StringComparison.Ordinal));
    SortKey = key ?? SortCodecov;
    IsDescending = key is not null && parts.Length == 2 && string.Equals(parts[1], Descending, StringComparison.Ordinal);
  }

  private void FlagChanged(string? flag) =>
    Navigation.NavigateTo(Navigation.GetUriWithQueryParameter("flag", string.IsNullOrEmpty(flag) ? null : flag));

  private void SearchChanged(string? search)
  {
    Search = search;
    BuildRows();
  }

  private void FilterChanged(string? filter)
  {
    Filter = string.IsNullOrEmpty(filter) ? FilterAll : filter;
    BuildRows();
    Navigation.NavigateTo(Navigation.GetUriWithQueryParameter("filter", Filter is FilterAll ? null : Filter), replace: true);
  }

  private void SortChanged(string? value)
  {
    ReadSort(value);
    ApplySort();
  }

  // A second click on the header of the sort reverses it; another header sorts ascending.
  private void SortBy(string key)
  {
    IsDescending = string.Equals(key, SortKey, StringComparison.Ordinal) && !IsDescending;
    SortKey = key;
    ApplySort();
  }

  private void ApplySort()
  {
    BuildRows();
    var value = SortKey is SortCodecov && !IsDescending ? null : SortValue;
    Navigation.NavigateTo(Navigation.GetUriWithQueryParameter("sort", value), replace: true);
  }

  private async Task ReprocessAsync()
  {
    var (forge, owner, name, sha) = (Forge, Owner, Name, Sha);
    Reprocessing = true;

    try
    {
      var result = await Runner.RunAsync((sender, ct) => sender.ReprocessCommitAsync(forge, owner, name, sha, ct)).ConfigureAwait(true);

      if (result is { Succeeded: true, Value: true })
      {
        Toasts.Show(CcTone.Success, L["Toast_ReprocessQueued"], ShortSha);
      }
      else if (result is { NotFound: true } or { Forbidden: true })
      {
        Toasts.Show(CcTone.Danger, L["Toast_RequestFailed"], L["Toast_ActionRefused"]);
      }
    }
    finally
    {
      Reprocessing = false;
    }
  }

  // The files shown: those of the search and the filter, in the order of the sort; the total is never sorted.
  private void BuildRows()
  {
    if (Report is not { } report)
    {
      Rows = [];
      return;
    }

    var term = Search?.Trim();
    var target = report.Target;
    var filter = Filter;
    var kept = report.Files.Where(
      row => (string.IsNullOrEmpty(term) || row.Path.Contains(term, StringComparison.OrdinalIgnoreCase))
             && filter switch
             {
               FilterBelowTarget => target is { } value && row.CodecovRatio < value,
               FilterUncoveredBranches => row.Branches is { } branches && branches.Covered < branches.Total,
               FilterUnexecutedMethods => row.Methods is { } methods && methods.Covered < methods.Total,
               var _ => true,
             });

    Rows = [.. Ordered(kept)];
  }

  // A row without value for the sort stays last in both directions; the ordinal path breaks ties.
  private IEnumerable<CommitReportRowViewModel> Ordered(IEnumerable<CommitReportRowViewModel> rows)
  {
    if (SortKey is SortPath)
    {
      return IsDescending
        ? rows.OrderByDescending(static r => r.Path, StringComparer.Ordinal)
        : rows.OrderBy(static r => r.Path, StringComparer.Ordinal);
    }

    var key = SortKey;
    var ordered = rows.OrderBy(r => RatioOf(r, key) is null);
    ordered = IsDescending ? ordered.ThenByDescending(r => RatioOf(r, key)) : ordered.ThenBy(r => RatioOf(r, key));

    return ordered.ThenBy(static r => r.Path, StringComparer.Ordinal);
  }

  private static decimal? RatioOf(CommitReportRowViewModel row, string key) => key switch
  {
    SortLines => row.Lines?.Ratio,
    SortBranches => row.Branches?.Ratio,
    SortMethods => row.Methods?.Ratio,
    var _ => row.CodecovRatio,
  };

  #endregion
}
