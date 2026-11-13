// ------------------------------------------------------------------------------------------------
// <copyright file="RepositoryHeader.razor.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.AspNetCore.Components;

using CodeCoverage.Web.Components.Ui.Models;
using CodeCoverage.Web.Mapping;
using CodeCoverage.Web.Messaging;

namespace CodeCoverage.Web.Components.Repositories;

/// <summary>
///   The header of every page of a repository: the crumbs (owner, repository, then the page), the title and the tabs of
///   the repository.
/// </summary>
/// <remarks>
///   The header asks once per repository whether the user administers it, and only then shows the settings tab. A
///   refusal or a failure shows no tab, which reveals nothing.
/// </remarks>
public partial class RepositoryHeader
{
  #region Fields

  private readonly LatestRequest _latest = new();

  // The address the permission was last asked for: a re-render of the same repository asks nothing again.
  private string? _asked;

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

  /// <summary>Gets or sets the title, or <see langword="null" /> for the name of the repository.</summary>
  [Parameter]
  public string? Title { get; set; }

  /// <summary>Gets or sets the line under the title.</summary>
  [Parameter]
  public string? Subtitle { get; set; }

  /// <summary>Gets or sets the crumbs after the repository, the last one being the page; none on the overview.</summary>
  [Parameter]
  public IReadOnlyList<CcBreadcrumbItem>? Crumbs { get; set; }

  /// <summary>Gets or sets the actions on the right of the title.</summary>
  [Parameter]
  public RenderFragment? Actions { get; set; }

  /// <summary>Gets or sets the tab the page imposes, or <see langword="null" /> to let the address decide.</summary>
  [Parameter]
  public RepositoryTab? ActiveTab { get; set; }

  #endregion

  #region Injected services

  [Inject]
  private UiRequestRunner Runner { get; set; } = null!;

  #endregion

  #region Properties

  private IReadOnlyList<CcBreadcrumbItem> Breadcrumb { get; set; } = [];

  private bool ShowSettings { get; set; }

  #endregion

  #region Lifecycle

  /// <inheritdoc />
  protected override async Task OnParametersSetAsync()
  {
    var (forge, owner, name) = (Forge, Owner, Name);
    var root = $"{Uri.EscapeDataString(forge)}/{Uri.EscapeDataString(owner)}/{Uri.EscapeDataString(name)}";

    // The owner leads to the repositories of its organization on the home page (?org={forge}/{login}), the repository
    // to its overview.
    Breadcrumb = [new CcBreadcrumbItem(owner, $"./?org={Uri.EscapeDataString($"{forge}/{owner}")}"), new CcBreadcrumbItem(name, root), .. Crumbs ?? []];

    if (string.Equals(_asked, root, StringComparison.Ordinal))
    {
      return;
    }

    _asked = root;
    ShowSettings = false;
    var ticket = _latest.Begin();
    var result = await Runner.RunAsync((sender, ct) => sender.CanAdministerRepositoryAsync(forge, owner, name, ct)).ConfigureAwait(true);

    // Another repository is shown since: its own answer says what its header shows.
    if (!_latest.IsLatest(ticket))
    {
      return;
    }

    ShowSettings = result is { Succeeded: true, Value: true };
  }

  #endregion
}
