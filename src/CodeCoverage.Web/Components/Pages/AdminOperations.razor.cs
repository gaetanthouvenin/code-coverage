// ------------------------------------------------------------------------------------------------
// <copyright file="AdminOperations.razor.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;

using CodeCoverage.Web.Components.Ui.Models;
using CodeCoverage.Web.Components.Ui.Services;
using CodeCoverage.Web.Mapping;
using CodeCoverage.Web.Messaging;
using CodeCoverage.Web.Resources;
using CodeCoverage.Web.ViewModels.Operations;

namespace CodeCoverage.Web.Components.Pages;

/// <summary>
///   The operations of the instance, metadata only and reserved to its administrators: queue, failed and dead jobs with
///   their retry, GitHub quota, latest webhook deliveries and worker heartbeats, read from the database by any process.
///   Failed uploads and their retry live on the page of a commit (M3.26, M3.27).
/// </summary>
public partial class AdminOperations
{
  #region Fields

  private readonly LatestRequest _latest = new();

  private bool _busy;

  #endregion

  #region Injected services

  [Inject]
  private UiRequestRunner Runner { get; set; } = null!;

  [Inject]
  private IToastService Toasts { get; set; } = null!;

  [Inject]
  private TimeProvider Time { get; set; } = null!;

  [Inject]
  private IStringLocalizer<Common> L { get; set; } = null!;

  #endregion

  #region Properties

  private PageState State { get; set; } = PageState.Loading;

  private OperationsViewModel? Status { get; set; }

  #endregion

  #region Lifecycle

  /// <inheritdoc />
  protected override Task OnInitializedAsync() => LoadAsync();

  #endregion

  #region Methods

  private async Task LoadAsync()
  {
    var now = Time.GetUtcNow();
    var ticket = _latest.Begin();
    var result = await Runner.RunAsync((sender, ct) => sender.GetOperationsViewModelAsync(now, ct)).ConfigureAwait(true);

    // Another refresh started since: its own load shows the newer state.
    if (!_latest.IsLatest(ticket))
    {
      return;
    }

    (State, Status) = result switch
    {
      { Succeeded: true } => (PageState.Ready, result.Value),
      { Forbidden: true } => (PageState.Denied, null),
      var _ => (PageState.Failed, Status),
    };
  }

  // The job is retried under its own organization by RetryJob; the list is read again, retried or refused.
  private async Task RetryAsync(JobRowViewModel job)
  {
    _busy = true;

    try
    {
      var result = await Runner.RunAsync((sender, ct) => sender.RetryJobAsync(job.Id, ct)).ConfigureAwait(true);

      if (result.Succeeded)
      {
        Toasts.Show(CcTone.Success, L["Operations_Retried"], job.Type);
      }

      await LoadAsync().ConfigureAwait(true);
    }
    finally
    {
      _busy = false;
    }
  }

  #endregion
}
