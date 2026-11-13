// ------------------------------------------------------------------------------------------------
// <copyright file="AnnotatedSource.razor.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Globalization;

using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;

using CodeCoverage.Web.Components.Ui.Models;
using CodeCoverage.Web.Components.Ui.Services;
using CodeCoverage.Web.Mapping;
using CodeCoverage.Web.Messaging;
using CodeCoverage.Web.Resources;
using CodeCoverage.Web.ViewModels.Source;

namespace CodeCoverage.Web.Components.Pages;

/// <summary>
///   The annotated source of a file at a commit: the totals of the file, then each line with its margin, its hits and
///   the patch of the pull request the page comes from (<c>?pull=</c>). The source is coloured once in the browser,
///   never through markup on the server: its text comes from the forge.
/// </summary>
/// <remarks>
///   One plain table with a key per line, no component per line and no virtualization, so a 5,000-line file renders
///   once and Ctrl+F and scrolling to a line keep working. The text of a code cell never changes for a given file, so
///   the renderer never touches what the script coloured; another file replaces the whole body.
/// </remarks>
public partial class AnnotatedSource : IAsyncDisposable
{
  #region Fields

  private const string Module = "./js/annotated-source.js";

  private readonly LatestRequest _latest = new();

  private ElementReference _table;

  private IJSObjectReference? _module;

  // The table last coloured: a re-render of it colours nothing again; a reload (flag, pull, retry) renders a new table,
  // which LoadAsync marks uncoloured.
  private string? _highlighted;

  // Set once the page left: an import still awaited then releases its module and colours nothing.
  private bool _disposed;

  #endregion

  #region Parameters

  /// <summary>Gets or sets the forge segment of the address.</summary>
  [Parameter]
  public string Forge { get; set; } = null!;

  /// <summary>Gets or sets the owner of the repository.</summary>
  [Parameter]
  public string Owner { get; set; } = null!;

  /// <summary>Gets or sets the name of the repository.</summary>
  [Parameter]
  public string Name { get; set; } = null!;

  /// <summary>Gets or sets the full SHA of the commit.</summary>
  [Parameter]
  public string Sha { get; set; } = null!;

  /// <summary>Gets or sets the path of the file, unescaped by the router.</summary>
  [Parameter]
  public string? Path { get; set; }

  /// <summary>Gets or sets the flag the coverage is restricted to (<c>?flag=</c>).</summary>
  [SupplyParameterFromQuery(Name = "flag")]
  public string? Flag { get; set; }

  /// <summary>Gets or sets the pull request the page comes from (<c>?pull=</c>); a value that is not positive is ignored.</summary>
  [SupplyParameterFromQuery(Name = "pull")]
  public int? Pull { get; set; }

  #endregion

  #region Injected services

  [Inject]
  private UiRequestRunner Runner { get; set; } = null!;

  [Inject]
  private IJSRuntime JS { get; set; } = null!;

  [Inject]
  private IToastService Toasts { get; set; } = null!;

  [Inject]
  private NavigationManager Navigation { get; set; } = null!;

  [Inject]
  private IStringLocalizer<Common> L { get; set; } = null!;

  #endregion

  #region Properties

  private PageState State { get; set; } = PageState.Loading;

  private AnnotatedSourceViewModel? Source { get; set; }

  private IReadOnlyList<CcBreadcrumbItem> Crumbs { get; set; } = [];

  private string Root => $"{Uri.EscapeDataString(Forge)}/{Uri.EscapeDataString(Owner)}/{Uri.EscapeDataString(Name)}";

  private string CommitHref => $"{Root}/commit/{Uri.EscapeDataString(Sha)}";

  private string ShortSha => Sha.Length > DisplayFormatter.ShortShaLength ? Sha[..DisplayFormatter.ShortShaLength] : Sha;

  private string FileName => Path is { Length: > 0 } path ? path[(path.LastIndexOf('/') + 1)..] : L["Source_Crumb"];

  // The body of the table belongs to one file: another one is rendered anew, never patched over coloured cells.
  private string LoadKey => $"{Sha}/{Path}";

  private IReadOnlyList<CcSelectOption> FlagOptions =>
    [new CcSelectOption(string.Empty, L["Filter_AllFlags"]), .. (Source?.Flags ?? []).Select(static f => new CcSelectOption(f, f))];

  #endregion

  #region Lifecycle

  /// <inheritdoc />
  protected override Task OnParametersSetAsync()
  {
    Crumbs = [new CcBreadcrumbItem(L["Tab_Commits"], $"{Root}/commits"), new CcBreadcrumbItem(ShortSha, CommitHref), new CcBreadcrumbItem(L["Source_Crumb"])];

    return LoadAsync();
  }

  /// <inheritdoc />
  protected override async Task OnAfterRenderAsync(bool firstRender)
  {
    // After a render of the circuit only (a prerender runs no OnAfterRender): its table replaces the prerendered one.
    if (State is not PageState.Ready || Source is not { IsSourceAvailable: true, SourceLines.Count: > 0 } || _highlighted == LoadKey)
    {
      return;
    }

    _highlighted = LoadKey;
    var path = Source.Path;

    if (await ModuleAsync().ConfigureAwait(true) is not { } module)
    {
      return;
    }

    try
    {
      await module.InvokeVoidAsync("highlight", _table, path).ConfigureAwait(true);
    }
    catch (JSDisconnectedException)
    {
      // The circuit is gone: nothing left to colour.
    }
  }

  #endregion

  #region Methods

  /// <inheritdoc />
  public async ValueTask DisposeAsync()
  {
    _disposed = true;
    _latest.Begin();

    if (_module is not null)
    {
      try
      {
        await _module.DisposeAsync().ConfigureAwait(true);
      }
      catch (JSDisconnectedException)
      {
        // The circuit is gone: nothing left to release.
      }
    }

    GC.SuppressFinalize(this);
  }

  private static string? StateOf(SourceLineState state) => state switch
  {
    SourceLineState.Hit => "hit",
    SourceLineState.Miss => "miss",
    SourceLineState.Partial => "partial",
    var _ => null,
  };

  // The module of the page, imported once; null once the page left, the module obtained meanwhile released.
  private async Task<IJSObjectReference?> ModuleAsync()
  {
    if (_module is not null || _disposed)
    {
      return _disposed ? null : _module;
    }

    IJSObjectReference module;

    try
    {
      module = await JS.InvokeAsync<IJSObjectReference>("import", Module).ConfigureAwait(true);
    }
    catch (JSDisconnectedException)
    {
      return null;
    }

    if (_disposed)
    {
      try
      {
        await module.DisposeAsync().ConfigureAwait(true);
      }
      catch (JSDisconnectedException)
      {
        // The circuit is gone: nothing left to release.
      }

      return null;
    }

    return _module = module;
  }

  private async Task LoadAsync()
  {
    State = PageState.Loading;

    // The spinner replaces the table: the one rendered next is new, and is coloured again.
    _highlighted = null;
    var (forge, owner, name, sha, path, flag, pull) = (Forge, Owner, Name, Sha, Path, Flag, Pull);
    var ticket = _latest.Begin();
    var result = await Runner.RunAsync((sender, ct) => sender.GetAnnotatedSourceViewModelAsync(forge, owner, name, sha, path, flag, pull, ct)).ConfigureAwait(true);

    // A navigation to another file, flag or pull request started since: its own load shows it.
    if (!_latest.IsLatest(ticket))
    {
      return;
    }

    (State, Source) = result switch
    {
      { Succeeded: true, Value: { } source } => (PageState.Ready, source),
      { Succeeded: true } or { NotFound: true } => (PageState.Missing, null),
      var _ => (PageState.Failed, Source),
    };
  }

  // « Covered · 3 », « Partial · 1/2 »: the state is also in the hits cell, never in the colour alone.
  private string? TitleOf(AnnotatedLineViewModel line) => line.State switch
  {
    SourceLineState.Hit => $"{L["Source_Covered"]} · {line.Hits}",
    SourceLineState.Miss => L["Source_NotCovered"],
    SourceLineState.Partial => $"{L["Source_Partial"]} · {line.Hits}",
    var _ => null,
  };

  private string PullHref(int pull) => string.Create(CultureInfo.InvariantCulture, $"{Root}/pull/{pull}");

  private void FlagChanged(string? flag) =>
    Navigation.NavigateTo(Navigation.GetUriWithQueryParameter("flag", string.IsNullOrEmpty(flag) ? null : flag));

  private async Task NextUncoveredAsync()
  {
    if (await ModuleAsync().ConfigureAwait(true) is not { } module)
    {
      return;
    }

    int line;

    try
    {
      line = await module.InvokeAsync<int>("nextUncovered", _table).ConfigureAwait(true);
    }
    catch (JSDisconnectedException)
    {
      return;
    }

    if (line == 0)
    {
      Toasts.Show(CcTone.Info, L["Source_NoUncovered"]);
    }
  }

  #endregion
}
