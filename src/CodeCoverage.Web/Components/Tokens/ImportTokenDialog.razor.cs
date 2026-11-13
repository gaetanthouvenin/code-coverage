// ------------------------------------------------------------------------------------------------
// <copyright file="ImportTokenDialog.razor.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.Localization;

using CodeCoverage.Web.Components.Ui.Models;
using CodeCoverage.Web.Components.Ui.Services;
using CodeCoverage.Web.Messaging;
using CodeCoverage.Web.Resources;

namespace CodeCoverage.Web.Components.Tokens;

/// <summary>
///   Imports a value the user already holds: the <c>CODECOV_TOKEN</c> of a pipeline, or the graphing token of a badge.
///   The value is typed, never shown back, and the dialog forgets it on close. An import never opens the dialog of an
///   issued value: the user already has it.
/// </summary>
public partial class ImportTokenDialog
{
  #region Fields

  private TokenForm _form = new();

  private EditContext _editContext = null!;

  private bool _busy;

  #endregion

  #region Parameters

  /// <summary>Gets or sets a value indicating whether the dialog is shown.</summary>
  [Parameter]
  public bool Open { get; set; }

  /// <summary>Gets or sets a value indicating whether a name is asked: an upload token has one, a badge token has none.</summary>
  [Parameter]
  public bool AskLabel { get; set; } = true;

  /// <summary>Gets or sets how the value is imported: the page knows what it belongs to.</summary>
  [Parameter, EditorRequired]
  public Func<ISender, string, string, CancellationToken, Task<bool>> Import { get; set; } = null!;

  /// <summary>Gets or sets what happens once the value is imported.</summary>
  [Parameter]
  public EventCallback OnImported { get; set; }

  /// <summary>Gets or sets what happens when the dialog closes without importing anything.</summary>
  [Parameter]
  public EventCallback OnClosed { get; set; }

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

  private bool IsFilled => !string.IsNullOrWhiteSpace(_form.ImportedValue) && (!AskLabel || !string.IsNullOrWhiteSpace(_form.Label));

  #endregion

  #region Lifecycle

  /// <inheritdoc />
  protected override void OnInitialized() => _editContext = new EditContext(_form);

  #endregion

  #region Methods

  private string? Error(string field) => _editContext.GetValidationMessages(_editContext.Field(field)).FirstOrDefault();

  private async Task ImportAsync()
  {
    if (!IsFilled)
    {
      return;
    }

    _busy = true;

    try
    {
      var (label, value) = (_form.Label, _form.ImportedValue);
      var result = await Runner.RunAsync((sender, ct) => Import(sender, label, value, ct), _editContext).ConfigureAwait(true);

      if (result.Succeeded)
      {
        Reset();
        Toasts.Show(CcTone.Success, L["Token_Imported"], label);
        await OnImported.InvokeAsync().ConfigureAwait(true);
      }
    }
    finally
    {
      _busy = false;
    }
  }

  private Task CloseAsync()
  {
    Reset();

    return OnClosed.InvokeAsync();
  }

  // The typed value never survives the dialog, refused or not.
  private void Reset()
  {
    _form = new TokenForm();
    _editContext = new EditContext(_form);
  }

  #endregion
}
