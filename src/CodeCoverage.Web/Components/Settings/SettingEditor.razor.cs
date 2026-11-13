// ------------------------------------------------------------------------------------------------
// <copyright file="SettingEditor.razor.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.Localization;

using CodeCoverage.Web.Components.Ui.Models;
using CodeCoverage.Web.Components.Ui.Services;
using CodeCoverage.Web.Mapping;
using CodeCoverage.Web.Messaging;
using CodeCoverage.Web.Resources;
using CodeCoverage.Web.ViewModels.Settings;

namespace CodeCoverage.Web.Components.Settings;

/// <summary>
///   The row of a setting: its key, its source, and the control of its kind (a field, a switch, several lines, or a
///   secret that is only replaced). It saves through the runner with its own form, so that the refusals of the server
///   show under it.
/// </summary>
public partial class SettingEditor : IDisposable
{
  #region Fields

  private SettingForm _form = new();

  private EditContext _editContext = null!;

  private bool _busy;

  private bool _disposed;

  #endregion

  #region Parameters

  /// <summary>Gets or sets the setting of the row.</summary>
  [Parameter, EditorRequired]
  public SettingViewModel Setting { get; set; } = null!;

  /// <summary>Gets or sets what happens once the setting changed on the server: the page reads the settings again.</summary>
  [Parameter]
  public EventCallback OnChanged { get; set; }

  /// <summary>
  ///   Gets or sets what happens when the server refuses a change to the user: the page shows its denied state, and
  ///   the row keeps the value of the server.
  /// </summary>
  [Parameter]
  public EventCallback OnDenied { get; set; }

  /// <summary>
  ///   Gets or sets what happens when the server answers that the target of the change no longer exists for the user (an
  ///   organization suspended, a membership lost): the page shows its inaccessible state. Unset, as on the settings of the
  ///   instance, nothing more happens.
  /// </summary>
  [Parameter]
  public EventCallback OnMissing { get; set; }

  /// <summary>Gets or sets what happens when the user asks to replace the secret of the row.</summary>
  [Parameter]
  public EventCallback<string> OnReplace { get; set; }

  /// <summary>Gets or sets how a value is saved: by default, as a setting of the instance.</summary>
  [Parameter]
  public Func<Mediator.ISender, string, string, CancellationToken, Task<bool>>? Save { get; set; }

  /// <summary>Gets or sets how a value is reset: by default, as a setting of the instance.</summary>
  [Parameter]
  public Func<Mediator.ISender, string, CancellationToken, Task<bool>>? Reset { get; set; }

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

  private bool IsOn => string.Equals(Setting.Value, "true", StringComparison.OrdinalIgnoreCase);

  private string? Error => _editContext.GetValidationMessages(_editContext.Field(nameof(SettingForm.Value))).FirstOrDefault();

  private string InputClass => Error is null ? "cc-input" : "cc-input cc-input--invalid";

  private string FieldClass => Setting.CanReset ? "cc-setting__field cc-setting__field--two" : "cc-setting__field";

  private CcTone OriginTone =>
    Setting.Origin switch
    {
      SettingOrigin.Database or SettingOrigin.Organization => CcTone.Info,
      SettingOrigin.Environment => CcTone.Warning,
      var _ => CcTone.Neutral,
    };

  private string OriginLabel =>
    Setting.Origin switch
    {
      SettingOrigin.Database => L["Setting_SourceDatabase"],
      SettingOrigin.Environment => L["Setting_SourceEnvironment"],
      SettingOrigin.Organization => L["Setting_SourceOrganization"],
      var _ => L["Setting_SourceDefault"],
    };

  #endregion

  #region Lifecycle

  /// <inheritdoc />
  protected override void OnParametersSet()
  {
    _form = new SettingForm { Value = Setting.Value ?? string.Empty };
    _editContext = new EditContext(_form);
  }

  #endregion

  #region Methods

  private Task SaveAsync() => SendAsync(L["Setting_Saved"], (sender, ct) => SaveValueAsync(sender, _form.Value, ct));

  private Task ToggleAsync(bool on) => SendAsync(L["Setting_Saved"], (sender, ct) => SaveValueAsync(sender, on ? "true" : "false", ct));

  private Task ResetAsync() =>
    SendAsync(
      Setting.Kind is SettingEditorKind.Secret ? L["Secret_Cleared"] : L["Setting_Reset"],
      (sender, ct) => Reset is { } reset ? reset(sender, Setting.Key, ct) : sender.ResetSettingAsync(Setting.Key, ct));

  private Task<bool> SaveValueAsync(Mediator.ISender sender, string value, CancellationToken cancellationToken) =>
    Save is { } save ? save(sender, Setting.Key, value, cancellationToken) : sender.UpdateSettingAsync(Setting.Key, value, cancellationToken);

  private Task OnReplaceClickedAsync() => OnReplace.InvokeAsync(Setting.Key);

  private async Task SendAsync(string done, Func<Mediator.ISender, CancellationToken, Task<bool>> request)
  {
    _busy = true;

    try
    {
      var result = await Runner.RunAsync(request, _editContext).ConfigureAwait(true);

      // The page moved on (another organization, another panel): this row no longer speaks for what it shows.
      if (_disposed)
      {
        return;
      }

      if (result.Succeeded)
      {
        Toasts.Show(CcTone.Success, done, Setting.Key);
        await OnChanged.InvokeAsync().ConfigureAwait(true);
      }
      else if (result.Forbidden)
      {
        // The typed value is dropped: the row shows the value of the server again.
        _form = new SettingForm { Value = Setting.Value ?? string.Empty };
        _editContext = new EditContext(_form);
        await OnDenied.InvokeAsync().ConfigureAwait(true);
      }
      else if (result.NotFound)
      {
        await OnMissing.InvokeAsync().ConfigureAwait(true);
      }
    }
    finally
    {
      _busy = false;
    }
  }

  #endregion

  #region Implementation of IDisposable

  /// <inheritdoc />
  public void Dispose()
  {
    _disposed = true;
    GC.SuppressFinalize(this);
  }

  #endregion
}
