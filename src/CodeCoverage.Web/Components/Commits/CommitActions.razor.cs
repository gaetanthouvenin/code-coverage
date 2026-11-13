// ------------------------------------------------------------------------------------------------
// <copyright file="CommitActions.razor.cs" company="Gaëtan Thouvenin">
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

namespace CodeCoverage.Web.Components.Commits;

/// <summary>
///   Re-notify and Reprocess, shown to the administrators of the repository only. Both only queue a job; the server
///   checks the right again, so hiding the buttons is no protection in itself.
/// </summary>
public partial class CommitActions
{
  #region Parameters

  /// <summary>Gets or sets the forge segment of the address.</summary>
  [Parameter, EditorRequired]
  public string Forge { get; set; } = null!;

  /// <summary>Gets or sets the owner of the repository.</summary>
  [Parameter, EditorRequired]
  public string Owner { get; set; } = null!;

  /// <summary>Gets or sets the name of the repository.</summary>
  [Parameter, EditorRequired]
  public string Name { get; set; } = null!;

  /// <summary>Gets or sets the full SHA of the commit.</summary>
  [Parameter, EditorRequired]
  public string Sha { get; set; } = null!;

  /// <summary>Gets or sets the short SHA of the commit, for the toasts.</summary>
  [Parameter, EditorRequired]
  public string ShortSha { get; set; } = null!;

  /// <summary>Gets or sets a value indicating whether the user administers the repository: the buttons are hidden otherwise.</summary>
  [Parameter]
  public bool CanAdminister { get; set; }

  /// <summary>Gets or sets the callback run once a reprocessing is queued, for the page to read the commit again.</summary>
  [Parameter]
  public EventCallback OnReprocessed { get; set; }

  #endregion

  #region Injected services

  [Inject]
  private UiRequestRunner Runner { get; set; } = null!;

  [Inject]
  private IToastService Toasts { get; set; } = null!;

  [Inject]
  private IStringLocalizer<Common> L { get; set; } = null!;

  #endregion

  #region Properties

  private bool Busy { get; set; }

  #endregion

  #region Methods

  private async Task NotifyAsync()
  {
    var (forge, owner, name, sha) = (Forge, Owner, Name, Sha);

    if (await RunAsync((sender, ct) => sender.TriggerNotifyAsync(forge, owner, name, sha, ct)).ConfigureAwait(true))
    {
      Toasts.Show(CcTone.Success, L["Toast_NotifyQueued"], L["Toast_NotifyQueuedText"]);
    }
  }

  private async Task ReprocessAsync()
  {
    var (forge, owner, name, sha) = (Forge, Owner, Name, Sha);

    if (await RunAsync((sender, ct) => sender.ReprocessCommitAsync(forge, owner, name, sha, ct)).ConfigureAwait(true))
    {
      Toasts.Show(CcTone.Success, L["Toast_ReprocessQueued"], ShortSha);
      await OnReprocessed.InvokeAsync().ConfigureAwait(true);
    }
  }

  // An action refused as missing or denied says so in a toast: the page itself stays as it is.
  private async Task<bool> RunAsync(Func<Mediator.ISender, CancellationToken, Task<bool>> request)
  {
    Busy = true;

    try
    {
      var result = await Runner.RunAsync(request).ConfigureAwait(true);

      if (result is { NotFound: true } or { Forbidden: true })
      {
        Toasts.Show(CcTone.Danger, L["Toast_RequestFailed"], L["Toast_ActionRefused"]);
      }

      return result is { Succeeded: true, Value: true };
    }
    finally
    {
      Busy = false;
    }
  }

  #endregion
}
