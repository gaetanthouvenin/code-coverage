// ------------------------------------------------------------------------------------------------
// <copyright file="Compare.razor.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;

using CodeCoverage.Web.Components.Ui.Models;
using CodeCoverage.Web.Mapping;
using CodeCoverage.Web.Messaging;
using CodeCoverage.Web.Resources;
using CodeCoverage.Web.ViewModels.Comparisons;

namespace CodeCoverage.Web.Components.Pages;

/// <summary>
///   The free comparison of two commits, <c>/compare/{base}...{head}</c>: the same content as a pull request, without its
///   comment, from the comparison a notification stored between them. Full SHAs only; a malformed range is refused
///   before any request, and a pair no notification compared says so, never computed here.
/// </summary>
/// <remarks>
///   The range is one route parameter split on <c>...</c>: the router takes no two parameters in one segment.
/// </remarks>
public partial class Compare
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

  /// <summary>Gets or sets the range of the address, <c>{base}...{head}</c>.</summary>
  [Parameter]
  public string Range { get; set; } = null!;

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

  private CompareViewModel? Commits { get; set; }

  private IReadOnlyList<CcBreadcrumbItem> Crumbs { get; set; } = [];

  // A malformed range or a short SHA: the form says which side is wrong, and nothing is asked of the server.
  private bool Invalid { get; set; }

  private string? BaseInput { get; set; }

  private string? HeadInput { get; set; }

  private string Title => Commits is { } commits ? $"{commits.Base} … {commits.Head}" : L["Compare_Title"];

  #endregion

  #region Lifecycle

  /// <inheritdoc />
  protected override Task OnParametersSetAsync()
  {
    Crumbs = [new CcBreadcrumbItem(L["Compare_Title"])];

    return LoadAsync();
  }

  #endregion

  #region Methods

  private static bool IsFull(string? value) => ComparisonMapper.IsFullSha(value?.Trim());

  private async Task LoadAsync()
  {
    var (forge, owner, name, range) = (Forge, Owner, Name, Range);
    var ticket = _latest.Begin();
    Invalid = !ComparisonMapper.TryParseRange(range, out var baseSha, out var headSha);

    if (Invalid)
    {
      (BaseInput, HeadInput, Commits, State) = (baseSha, headSha, null, PageState.Ready);
      return;
    }

    State = PageState.Loading;
    var result = await Runner.RunAsync((sender, ct) => sender.GetCompareViewModelAsync(forge, owner, name, range, ct)).ConfigureAwait(true);

    // A navigation to another range started since: its own load shows it.
    if (!_latest.IsLatest(ticket))
    {
      return;
    }

    (State, Commits) = result switch
    {
      { Succeeded: true, Value: { } commits } => (PageState.Ready, commits),
      { Succeeded: true } or { NotFound: true } => (PageState.Missing, null),
      var _ => (PageState.Failed, Commits),
    };
  }

  private void SubmitRange()
  {
    var (baseSha, headSha) = (BaseInput?.Trim(), HeadInput?.Trim());

    if (!IsFull(baseSha) || !IsFull(headSha))
    {
      return;
    }

    var root = $"{Uri.EscapeDataString(Forge)}/{Uri.EscapeDataString(Owner)}/{Uri.EscapeDataString(Name)}";
    Navigation.NavigateTo($"{root}/compare/{baseSha}{ComparisonMapper.RangeSeparator}{headSha}");
  }

  #endregion
}
