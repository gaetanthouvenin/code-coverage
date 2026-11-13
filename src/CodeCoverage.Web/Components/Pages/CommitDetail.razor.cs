// ------------------------------------------------------------------------------------------------
// <copyright file="CommitDetail.razor.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Globalization;

using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;

using CodeCoverage.Web.Components.Ui.Models;
using CodeCoverage.Web.Components.Ui.Services;
using CodeCoverage.Web.Mapping;
using CodeCoverage.Web.Messaging;
using CodeCoverage.Web.Resources;
using CodeCoverage.Web.ViewModels.Commits;

namespace CodeCoverage.Web.Components.Pages;

/// <summary>
///   The summary of a commit: totals against its base, flags with the marker of a carried forward one, published
///   statuses, and the source and warnings of the configuration stored for it, with a banner when it could not be read.
///   A SHA that is not a full one shows the inaccessible state.
/// </summary>
public partial class CommitDetail
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

  /// <summary>Gets or sets the full SHA of the commit.</summary>
  [Parameter]
  public string Sha { get; set; } = null!;

  #endregion

  #region Injected services

  [Inject]
  private UiRequestRunner Runner { get; set; } = null!;

  [Inject]
  private TimeProvider Time { get; set; } = null!;

  [Inject]
  private IStringLocalizer<Common> L { get; set; } = null!;

  [Inject]
  private IToastService Toasts { get; set; } = null!;

  #endregion

  #region Properties

  private PageState State { get; set; } = PageState.Loading;

  private CommitDetailViewModel? Detail { get; set; }

  private IReadOnlyList<CcBreadcrumbItem> Crumbs { get; set; } = [];

  // The answer the header read once for the page: the actions show only on a true answer.
  private bool CanAdminister { get; set; }

  private string? RetryingKey { get; set; }

  private IReadOnlyList<string> FlagNames => Detail is null ? [] : [.. Detail.Flags.Select(static f => f.Flag)];

  // « e4f5a6b on feature/webhook-retry · jo-rivera · 12 min ago », then « · #423 » linked to the pull request.
  private string? Subtitle =>
    Detail is not { } detail
      ? null
      : string.Join(
        " · ",
        new[]
          {
            detail.Branch is { } branch ? L["Commit_OnBranch", detail.ShortSha, branch].Value : detail.ShortSha,
            detail.Author,
            DisplayFormatter.Age(detail.At, Time.GetUtcNow(), L),
          }.OfType<string>());

  #endregion

  #region Lifecycle

  /// <inheritdoc />
  protected override Task OnParametersSetAsync()
  {
    var root = $"{Uri.EscapeDataString(Forge)}/{Uri.EscapeDataString(Owner)}/{Uri.EscapeDataString(Name)}";
    Crumbs = [new CcBreadcrumbItem(L["Tab_Commits"], $"{root}/commits"), new CcBreadcrumbItem(Sha.Length > DisplayFormatter.ShortShaLength ? Sha[..DisplayFormatter.ShortShaLength] : Sha)];

    return LoadAsync();
  }

  #endregion

  #region Methods

  private Task LoadAsync()
  {
    State = PageState.Loading;

    return ReadAsync();
  }

  // After an action, the commit is read again without leaving what is shown.
  private Task ReloadAsync() => ReadAsync();

  private string PullHref(int number) =>
    string.Create(CultureInfo.InvariantCulture, $"{Uri.EscapeDataString(Forge)}/{Uri.EscapeDataString(Owner)}/{Uri.EscapeDataString(Name)}/pull/{number}");

  private void PermissionKnown(bool canAdminister) => CanAdminister = canAdminister;

  private async Task RetryAsync(UploadSessionRowViewModel row)
  {
    if (row.UploadId is not { } uploadId)
    {
      return;
    }

    var (forge, owner, name) = (Forge, Owner, Name);
    RetryingKey = row.Key;

    try
    {
      var result = await Runner.RunAsync((sender, ct) => sender.RetryUploadAsync(forge, owner, name, uploadId, ct)).ConfigureAwait(true);

      if (result is { Succeeded: true, Value: true })
      {
        Toasts.Show(CcTone.Success, L["Toast_RetryQueued"], row.Name ?? L["Upload_Number", row.Number].Value);
      }
      else if (result is { NotFound: true } or { Forbidden: true })
      {
        // A refused action never turns the page into its missing or denied state.
        Toasts.Show(CcTone.Danger, L["Toast_RequestFailed"], L["Toast_ActionRefused"]);
      }
    }
    finally
    {
      RetryingKey = null;
    }

    await ReloadAsync().ConfigureAwait(true);
  }

  private async Task ReadAsync()
  {
    var (forge, owner, name, sha) = (Forge, Owner, Name, Sha);
    var ticket = _latest.Begin();
    var result = await Runner.RunAsync((sender, ct) => sender.GetCommitDetailViewModelAsync(forge, owner, name, sha, ct)).ConfigureAwait(true);

    // A navigation to another commit started since: its own load shows it.
    if (!_latest.IsLatest(ticket))
    {
      return;
    }

    (State, Detail) = result switch
    {
      { Succeeded: true, Value: { } detail } => (PageState.Ready, detail),
      { Succeeded: true } or { NotFound: true } => (PageState.Missing, null),
      var _ => (PageState.Failed, Detail),
    };
  }

  #endregion
}
