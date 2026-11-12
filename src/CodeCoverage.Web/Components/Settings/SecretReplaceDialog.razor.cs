// ------------------------------------------------------------------------------------------------
// <copyright file="SecretReplaceDialog.razor.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.Localization;

using CodeCoverage.Web.Components.Ui;
using CodeCoverage.Web.Components.Ui.Models;
using CodeCoverage.Web.Components.Ui.Services;
using CodeCoverage.Web.Mapping;
using CodeCoverage.Web.Messaging;
using CodeCoverage.Web.Resources;

namespace CodeCoverage.Web.Components.Settings;

/// <summary>
///   Replaces a secret of the instance: the current value is never shown, and the typed one is forgotten as soon as the
///   dialog closes, whether the replacement succeeded or not.
/// </summary>
public partial class SecretReplaceDialog
{
  #region Fields

  private readonly SettingForm _form = new();

  private EditContext _editContext = null!;

  private CcDialog? _dialog;

  private string? _value;

  private bool _busy;

  #endregion

  #region Parameters

  /// <summary>Gets or sets the key of the secret to replace, or <see langword="null" /> while the dialog is closed.</summary>
  [Parameter]
  public string? Key { get; set; }

  /// <summary>Gets or sets what happens when the server refuses the replacement to the user, once the dialog closed.</summary>
  [Parameter]
  public EventCallback OnDenied { get; set; }

  /// <summary>Gets or sets what happens when the dialog closes, with whether the secret was replaced.</summary>
  [Parameter]
  public EventCallback<bool> OnClosed { get; set; }

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

  private string Title => $"{L["Action_Replace"]} {Key}";

  private string? Error => _editContext.GetValidationMessages(_editContext.Field(nameof(SettingForm.Value))).FirstOrDefault();

  #endregion

  #region Lifecycle

  /// <inheritdoc />
  protected override void OnInitialized() => _editContext = new EditContext(_form);

  #endregion

  #region Methods

  private async Task ReplaceAsync()
  {
    if (Key is not { } key || string.IsNullOrEmpty(_value))
    {
      return;
    }

    _busy = true;

    try
    {
      var value = _value;
      var result = await Runner.RunAsync((sender, ct) => sender.ReplaceSecretAsync(key, value, ct), _editContext).ConfigureAwait(true);

      if (result.Succeeded)
      {
        Toasts.Show(CcTone.Success, L["Secret_Replaced"], key);
        await FinishAsync(replaced: true).ConfigureAwait(true);
      }
      else if (result.Forbidden)
      {
        await FinishAsync(replaced: false).ConfigureAwait(true);
        await OnDenied.InvokeAsync().ConfigureAwait(true);
      }
      else
      {
        // A refused value is not kept for another try: it is typed again, and the focus goes back to the field (the
        // focused Replace button is disabled meanwhile).
        _value = null;
        _busy = false;
        StateHasChanged();

        if (_dialog is not null)
        {
          await _dialog.FocusAsync().ConfigureAwait(true);
        }
      }
    }
    finally
    {
      _busy = false;
    }
  }

  private Task CloseAsync() => FinishAsync(replaced: false);

  private Task FinishAsync(bool replaced)
  {
    _value = null;

    return OnClosed.InvokeAsync(replaced);
  }

  #endregion
}
