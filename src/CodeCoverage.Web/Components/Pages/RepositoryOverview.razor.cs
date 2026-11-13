// ------------------------------------------------------------------------------------------------
// <copyright file="RepositoryOverview.razor.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;

using CodeCoverage.Application.Configuration;

using CodeCoverage.Web.Mapping;
using CodeCoverage.Web.Messaging;
using CodeCoverage.Web.Resources;
using CodeCoverage.Web.ViewModels.Repositories;

namespace CodeCoverage.Web.Components.Pages;

/// <summary>
///   The overview of a repository: the cards of the project and of every flag, with their change over 30 days, and the
///   chart of the history of a branch. An unknown, unreadable or foreign repository, and a forge the console does not
///   serve, show the same inaccessible state.
/// </summary>
public partial class RepositoryOverview
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

  #endregion

  #region Injected services

  [Inject]
  private UiRequestRunner Runner { get; set; } = null!;

  [Inject]
  private IStringLocalizer<Common> L { get; set; } = null!;

  [Inject]
  private IOptionsMonitor<UrlOptions> Urls { get; set; } = null!;

  #endregion

  #region Properties

  private PageState State { get; set; } = PageState.Loading;

  private RepositoryOverviewViewModel? Overview { get; set; }

  private RepositoryActivityViewModel? Activity { get; set; }

  private string? Subtitle => Overview is { } loaded ? L["Repository_DefaultBranch", loaded.DefaultBranch].Value : null;

  #endregion

  #region Lifecycle

  /// <inheritdoc />
  protected override Task OnParametersSetAsync()
  {
    State = PageState.Loading;
    (Overview, Activity) = (null, null);

    return LoadAsync();
  }

  #endregion

  #region Methods

  private async Task LoadAsync()
  {
    var (forge, owner, name) = (Forge, Owner, Name);
    var ticket = _latest.Begin();
    var result = await Runner.RunAsync((sender, ct) => sender.GetRepositoryOverviewViewModelAsync(forge, owner, name, ct)).ConfigureAwait(true);

    // A navigation to another repository started since: its own load shows it.
    if (!_latest.IsLatest(ticket))
    {
      return;
    }

    (State, Overview) = result switch
    {
      { Succeeded: true, Value: { } overview } => (PageState.Ready, overview),
      { Succeeded: true } or { NotFound: true } => (PageState.Missing, null),
      var _ => (PageState.Failed, Overview),
    };

    if (Overview is { HasCoverage: true } loaded)
    {
      await LoadActivityAsync(ticket, loaded).ConfigureAwait(true);
    }
  }

  // The panels of the lower half are read after the cards, with what the cards already know: the repository and its
  // configuration are not read again.
  private async Task LoadActivityAsync(long ticket, RepositoryOverviewViewModel overview)
  {
    var (forge, owner, name) = (Forge, Owner, Name);
    var urls = Urls.CurrentValue;
    var result = await Runner
                      .RunAsync((sender, ct) => sender.GetRepositoryActivityViewModelAsync(
                                  forge,
                                  owner,
                                  name,
                                  overview.DefaultBranch,
                                  overview.Format,
                                  urls.ApiPublicUrl,
                                  urls.WebPublicUrl,
                                  ct))
                      .ConfigureAwait(true);

    // A navigation to another repository started since: its own load shows it.
    if (!_latest.IsLatest(ticket))
    {
      return;
    }

    Activity = result is { Succeeded: true, Value: { } activity } ? activity : null;
  }

  #endregion
}
