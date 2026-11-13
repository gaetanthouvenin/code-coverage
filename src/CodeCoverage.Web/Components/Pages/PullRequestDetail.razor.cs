// ------------------------------------------------------------------------------------------------
// <copyright file="PullRequestDetail.razor.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Globalization;

using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;

using CodeCoverage.Web.Components.Ui.Models;
using CodeCoverage.Web.Mapping;
using CodeCoverage.Web.Messaging;
using CodeCoverage.Web.Resources;
using CodeCoverage.Web.ViewModels.Comparisons;

namespace CodeCoverage.Web.Components.Pages;

/// <summary>
///   The page of a pull request: how its base was chosen, its patch, its project Δ and its <c>diff</c> table, from the
///   comparison stored for its head. A readable pull request whose head has none yet says the comparison is pending.
/// </summary>
public partial class PullRequestDetail
{
  #region Fields

  private readonly LatestRequest _latest = new();

  // The pull request the page shows or loads.
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

  /// <summary>Gets or sets the number of the pull request.</summary>
  [Parameter]
  public int Number { get; set; }

  #endregion

  #region Injected services

  [Inject]
  private UiRequestRunner Runner { get; set; } = null!;

  [Inject]
  private IStringLocalizer<Common> L { get; set; } = null!;

  #endregion

  #region Properties

  private PageState State { get; set; } = PageState.Loading;

  private PullRequestDetailViewModel? Detail { get; set; }

  private IReadOnlyList<CcBreadcrumbItem> Crumbs { get; set; } = [];

  private string NumberText => string.Create(CultureInfo.InvariantCulture, $"#{Number}");

  // « #423 feat(alerts): retry policy for failed webhooks », the number alone until it is read.
  private string Title => Detail is { } detail ? $"{NumberText} {detail.Title}" : NumberText;

  #endregion

  #region Lifecycle

  /// <inheritdoc />
  protected override Task OnParametersSetAsync()
  {
    var root = $"{Uri.EscapeDataString(Forge)}/{Uri.EscapeDataString(Owner)}/{Uri.EscapeDataString(Name)}";
    Crumbs = [new CcBreadcrumbItem(L["Tab_Pulls"], $"{root}/pulls"), new CcBreadcrumbItem(NumberText)];

    return LoadAsync();
  }

  #endregion

  #region Methods

  private async Task LoadAsync()
  {
    State = PageState.Loading;
    var (forge, owner, name, number) = (Forge, Owner, Name, Number);
    var key = $"{forge}/{owner}/{name}/{number}";

    // Another pull request: nothing of the previous one is shown or acted on while this one loads.
    if (!string.Equals(_loaded, key, StringComparison.Ordinal))
    {
      (_loaded, Detail) = (key, null);
    }

    var ticket = _latest.Begin();
    var result = await Runner.RunAsync((sender, ct) => sender.GetPullRequestDetailViewModelAsync(forge, owner, name, number, ct)).ConfigureAwait(true);

    // A navigation to another pull request started since: its own load shows it.
    if (!_latest.IsLatest(ticket))
    {
      return;
    }

    (State, Detail) = result switch
    {
      { Succeeded: true, Value: { } detail } => (PageState.Ready, detail),
      { Succeeded: true } or { NotFound: true } => (PageState.Missing, null),
      var _ => (PageState.Failed, null),
    };
  }

  #endregion
}
