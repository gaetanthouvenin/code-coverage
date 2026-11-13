// ------------------------------------------------------------------------------------------------
// <copyright file="TokenTable.razor.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Globalization;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Localization;

using CodeCoverage.Web.Resources;
using CodeCoverage.Web.ViewModels.Tokens;

namespace CodeCoverage.Web.Components.Tokens;

/// <summary>
///   The tokens of a repository, of an organization or of an account: their name, what recognizes them, when they were
///   created and last used. A revoked token is hidden behind a toggle, since it can no longer be used.
/// </summary>
public partial class TokenTable
{
  #region Parameters

  /// <summary>Gets or sets the title of the panel.</summary>
  [Parameter, EditorRequired]
  public string Title { get; set; } = null!;

  /// <summary>Gets or sets the tokens to show, revoked ones included.</summary>
  [Parameter, EditorRequired]
  public IReadOnlyList<UploadTokenViewModel> Tokens { get; set; } = [];

  /// <summary>Gets or sets what the empty state says.</summary>
  [Parameter, EditorRequired]
  public string EmptyDescription { get; set; } = null!;

  /// <summary>Gets or sets the actions of the panel, beside the toggle.</summary>
  [Parameter]
  public RenderFragment? Tools { get; set; }

  /// <summary>Gets or sets what happens when the user asks to revoke a token.</summary>
  [Parameter]
  public EventCallback<UploadTokenViewModel> OnRevokeAsked { get; set; }

  #endregion

  #region Injected services

  [Inject]
  private IStringLocalizer<Common> L { get; set; } = null!;

  #endregion

  #region Properties

  private bool ShowRevoked { get; set; }

  private bool HasRevoked => Tokens.Any(static t => t.IsRevoked);

  private IReadOnlyList<UploadTokenViewModel> Shown => ShowRevoked ? Tokens : [.. Tokens.Where(static t => !t.IsRevoked)];

  #endregion

  #region Methods

  private static string Date(DateTimeOffset at) => at.ToLocalTime().ToString("d", CultureInfo.CurrentCulture);

  private void ToggleRevokedAsync(ChangeEventArgs e) => ShowRevoked = e.Value is true;

  private Task AskRevokeAsync(UploadTokenViewModel token) => OnRevokeAsked.InvokeAsync(token);

  #endregion
}
