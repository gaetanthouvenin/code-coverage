// ------------------------------------------------------------------------------------------------
// <copyright file="AdminOrganizations.razor.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.Localization;

using CodeCoverage.Web.Components.Organizations;
using CodeCoverage.Web.Components.Ui.Models;
using CodeCoverage.Web.Components.Ui.Services;
using CodeCoverage.Web.Mapping;
using CodeCoverage.Web.Messaging;
using CodeCoverage.Web.Resources;
using CodeCoverage.Web.ViewModels.Organizations;

namespace CodeCoverage.Web.Components.Pages;

/// <summary>
///   The organizations of the instance and their lifecycle (admit, suspend): reserved to the administrators of the
///   instance. It shows the rows of the organizations and the depth of their queue, never their data.
/// </summary>
public partial class AdminOrganizations
{
  #region Fields

  private const string Dash = "—";

  private AdmitAccountForm _admit = new();

  private EditContext _admitContext = null!;

  private bool _admitOpen;

  private bool _busy;

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

  private PageState State { get; set; } = PageState.Loading;

  private IReadOnlyList<OrganizationRowViewModel>? Rows { get; set; }

  private string? Filter { get; set; }

  private OrganizationRowViewModel? Suspended { get; set; }

  private int PendingCount => Rows?.Count(static r => r.State is OrganizationLifecycle.Pending) ?? 0;

  // The search keeps the logins that contain it, whatever its case.
  private IEnumerable<OrganizationRowViewModel> VisibleRows =>
    string.IsNullOrWhiteSpace(Filter) ? Rows ?? [] : (Rows ?? []).Where(r => r.Login.Contains(Filter.Trim(), StringComparison.OrdinalIgnoreCase));

  private IReadOnlyList<CcSelectOption> Forges { get; set; } = [];

  private string? AdmitForge
  {
    get => _admit.Forge;
    set => _admit.Forge = value ?? "github";
  }

  #endregion

  #region Lifecycle

  /// <inheritdoc />
  protected override Task OnInitializedAsync()
  {
    // GitHub only until M8 and M9 bring the other forges.
    Forges = [new CcSelectOption("github", L["Forge_GitHub"])];
    _admitContext = new EditContext(_admit);

    return LoadAsync();
  }

  #endregion

  #region Methods

  private static string Mark(string login) => login.Length <= 2 ? login.ToUpperInvariant() : login[..2].ToUpperInvariant();

  private async Task LoadAsync()
  {
    var result = await Runner.RunAsync(static (sender, ct) => sender.GetOrganizationRowsViewModelAsync(ct)).ConfigureAwait(true);

    (State, Rows) = result switch
    {
      { Succeeded: true } => (PageState.Ready, result.Value),
      { Forbidden: true } => (PageState.Denied, null),
      var _ => (PageState.Failed, Rows),
    };
  }

  private Task AdmitAsync(OrganizationRowViewModel row) =>
    SendAsync(L["AdminOrganizations_Admitted"], row.Login, (sender, ct) => sender.AdmitOrganizationAsync(row.Forge, row.Login, row.ExternalAccountId, ct), null);

  private async Task AdmitTypedAsync()
  {
    var login = _admit.Login ?? string.Empty;
    var account = _admit.ExternalAccountId ?? string.Empty;
    var forge = _admit.Forge;

    if (await SendAsync(L["AdminOrganizations_Admitted"], login, (sender, ct) => sender.AdmitOrganizationAsync(forge, login, account, ct), _admitContext).ConfigureAwait(true))
    {
      CloseAdmit();
    }
  }

  private async Task SuspendAsync()
  {
    if (Suspended is not { } row)
    {
      return;
    }

    if (await SendAsync(L["AdminOrganizations_Suspended"], row.Login, (sender, ct) => sender.SuspendOrganizationAsync(row.Forge, row.Login, ct), null).ConfigureAwait(true))
    {
      CloseSuspend();
    }
  }

  // After every change the rows are read again, refused or not: a concurrent admission shows at once.
  private async Task<bool> SendAsync(string done, string login, Func<Mediator.ISender, CancellationToken, Task<bool>> request, EditContext? form)
  {
    _busy = true;

    try
    {
      var result = await Runner.RunAsync(request, form).ConfigureAwait(true);

      if (result.Succeeded)
      {
        Toasts.Show(CcTone.Success, done, login);
      }

      await LoadAsync().ConfigureAwait(true);

      return result.Succeeded;
    }
    finally
    {
      _busy = false;
    }
  }

  private string? ErrorOf(string field) => _admitContext.GetValidationMessages(_admitContext.Field(field)).FirstOrDefault();

  private void OpenAdmit()
  {
    _admit = new AdmitAccountForm();
    _admitContext = new EditContext(_admit);
    _admitOpen = true;
  }

  private void CloseAdmit() => _admitOpen = false;

  private void OpenSuspend(OrganizationRowViewModel row) => Suspended = row;

  private void CloseSuspend() => Suspended = null;

  #endregion
}
