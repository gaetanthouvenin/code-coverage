// ------------------------------------------------------------------------------------------------
// <copyright file="RepositoryUploadTokens.razor.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;

using CodeCoverage.Web.Mapping;
using CodeCoverage.Web.Resources;
using CodeCoverage.Web.ViewModels.Tokens;

namespace CodeCoverage.Web.Components.Repositories;

/// <summary>
///   The upload tokens of a repository: created with a name, imported from Codecov so that pipelines keep their
///   secret, and revoked. The value of a created token is shown once, by the dialog, and forgotten when it closes.
/// </summary>
public partial class RepositoryUploadTokens
{
  #region Fields

  private bool _creating;

  private bool _importing;

  private UploadTokenViewModel? _revoking;

  private IssuedTokenViewModel? _issued;

  #endregion

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

  /// <summary>Gets or sets the tokens of the repository, the revoked ones included.</summary>
  [Parameter, EditorRequired]
  public IReadOnlyList<UploadTokenViewModel> Tokens { get; set; } = [];

  /// <summary>Gets or sets what happens when the list changed on the server: the page reads it again.</summary>
  [Parameter]
  public EventCallback OnChanged { get; set; }

  #endregion

  #region Injected services

  [Inject]
  private IStringLocalizer<Common> L { get; set; } = null!;

  #endregion

  #region Methods

  private Task<IssuedTokenViewModel?> CreateAsync(ISender sender, string label, CancellationToken cancellationToken) =>
    sender.CreateRepositoryUploadTokenAsync(Forge, Owner, Name, label, cancellationToken);

  private Task<bool> ImportAsync(ISender sender, string label, string value, CancellationToken cancellationToken) =>
    sender.ImportRepositoryUploadTokenAsync(Forge, Owner, Name, label, value, cancellationToken);

  private Task<bool> RevokeAsync(ISender sender, long tokenId, CancellationToken cancellationToken) =>
    sender.RevokeRepositoryUploadTokenAsync(Forge, Owner, Name, tokenId, cancellationToken);

  private void AskRevoke(UploadTokenViewModel token) => _revoking = token;

  private async Task ShowIssuedAsync(IssuedTokenViewModel issued)
  {
    (_creating, _issued) = (false, issued);

    await OnChanged.InvokeAsync().ConfigureAwait(true);
  }

  // The value is dropped with the dialog: nothing can show it again.
  private Task ForgetIssuedAsync()
  {
    _issued = null;

    return Task.CompletedTask;
  }

  private async Task ImportedAsync()
  {
    _importing = false;

    await OnChanged.InvokeAsync().ConfigureAwait(true);
  }

  private async Task RevokedAsync()
  {
    _revoking = null;

    await OnChanged.InvokeAsync().ConfigureAwait(true);
  }

  #endregion
}
