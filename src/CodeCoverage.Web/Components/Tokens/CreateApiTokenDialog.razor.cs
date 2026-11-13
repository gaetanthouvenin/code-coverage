// ------------------------------------------------------------------------------------------------
// <copyright file="CreateApiTokenDialog.razor.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.Localization;

using CodeCoverage.Web.Components.Ui.Models;
using CodeCoverage.Web.Mapping;
using CodeCoverage.Web.Messaging;
using CodeCoverage.Web.Resources;
using CodeCoverage.Web.ViewModels.Tokens;

namespace CodeCoverage.Web.Components.Tokens;

/// <summary>
///   Creates a personal token of the API. A token belongs to exactly one organization, chosen here; the instance
///   scope is offered to the administrators of the instance only. The value is given back once, to the dialog that
///   shows it.
/// </summary>
public partial class CreateApiTokenDialog
{
  #region Fields

  /// <summary>The value of the option of the instance scope, which no login can take.</summary>
  private const string InstanceScope = "/instance";

  private readonly ApiTokenForm _form = new();

  private EditContext _editContext = null!;

  private string? _name;

  private string? _scope;

  private bool _busy;

  #endregion

  #region Parameters

  /// <summary>Gets or sets a value indicating whether the dialog is shown.</summary>
  [Parameter]
  public bool Open { get; set; }

  /// <summary>Gets or sets the organizations a token may be scoped to.</summary>
  [Parameter, EditorRequired]
  public IReadOnlyList<TokenScopeViewModel> Organizations { get; set; } = [];

  /// <summary>Gets or sets a value indicating whether the instance scope is offered.</summary>
  [Parameter]
  public bool CanScopeToInstance { get; set; }

  /// <summary>Gets or sets what happens once the token is created, with its value shown this one time.</summary>
  [Parameter]
  public EventCallback<IssuedTokenViewModel> OnCreated { get; set; }

  /// <summary>Gets or sets what happens when the dialog closes without creating anything.</summary>
  [Parameter]
  public EventCallback OnClosed { get; set; }

  #endregion

  #region Injected services

  [Inject]
  private UiRequestRunner Runner { get; set; } = null!;

  [Inject]
  private IStringLocalizer<Common> L { get; set; } = null!;

  #endregion

  #region Properties

  private string? Error => _editContext.GetValidationMessages(_editContext.Field(nameof(ApiTokenForm.Name))).FirstOrDefault();

  private IReadOnlyList<CcSelectOption> Scopes =>
  [
    .. Organizations.Select(static o => new CcSelectOption(o.Login, o.Login)),
    .. CanScopeToInstance ? new[] { new CcSelectOption(InstanceScope, L["Nav_Instance"]) } : [],
  ];

  #endregion

  #region Lifecycle

  /// <inheritdoc />
  protected override void OnInitialized() => _editContext = new EditContext(_form);

  /// <inheritdoc />
  protected override void OnParametersSet() => _scope ??= Scopes.FirstOrDefault()?.Value;

  #endregion

  #region Methods

  private async Task CreateAsync()
  {
    if (string.IsNullOrWhiteSpace(_name) || _scope is not { } scope)
    {
      return;
    }

    _busy = true;

    try
    {
      var name = _name;
      var instance = string.Equals(scope, InstanceScope, StringComparison.Ordinal);
      var forge = instance ? null : Organizations.FirstOrDefault(o => string.Equals(o.Login, scope, StringComparison.Ordinal))?.Forge;
      var login = instance ? null : scope;
      var result = await Runner.RunAsync((sender, ct) => Create(sender, name, forge, login, ct), _editContext).ConfigureAwait(true);

      if (result is { Succeeded: true, Value: { } issued })
      {
        _name = null;
        await OnCreated.InvokeAsync(issued).ConfigureAwait(true);
      }
    }
    finally
    {
      _busy = false;
    }
  }

  private static Task<IssuedTokenViewModel?> Create(ISender sender, string name, string? forge, string? login, CancellationToken cancellationToken) =>
    sender.CreateApiTokenAsync(name, forge, login, cancellationToken);

  private Task CloseAsync()
  {
    _name = null;

    return OnClosed.InvokeAsync();
  }

  #endregion
}
