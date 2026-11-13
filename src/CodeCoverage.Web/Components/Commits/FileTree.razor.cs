// ------------------------------------------------------------------------------------------------
// <copyright file="FileTree.razor.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;

using CodeCoverage.Web.Components.Pages;
using CodeCoverage.Web.Components.Ui.Models;
using CodeCoverage.Web.Mapping;
using CodeCoverage.Web.Messaging;
using CodeCoverage.Web.Resources;
using CodeCoverage.Web.ViewModels.Commits;
using CodeCoverage.Web.ViewModels.Repositories;

namespace CodeCoverage.Web.Components.Commits;

/// <summary>
///   The files of a commit as a tree: folders collapsed below the first level, path order or coverage order within each
///   level, a flag filter that reads the tree again and a search that lists the matching files flat.
/// </summary>
public partial class FileTree
{
  #region Fields

  /// <summary>The largest number of files a search lists.</summary>
  internal const int MaxSearchRows = 100;

  private readonly LatestRequest _latest = new();

  // The commit and flag the tree was last read for: a re-render of the same asks nothing again.
  private string? _loaded;

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

  /// <summary>Gets or sets the full SHA of the commit.</summary>
  [Parameter, EditorRequired]
  public string Sha { get; set; } = null!;

  /// <summary>Gets or sets the flags of the commit, the only ones the filter offers.</summary>
  [Parameter]
  public IReadOnlyList<string> Flags { get; set; } = [];

  /// <summary>Gets or sets how the repository writes a coverage.</summary>
  [Parameter, EditorRequired]
  public CoverageFormatViewModel Format { get; set; } = null!;

  /// <summary>Gets or sets a value indicating whether the commit was compacted: only its totals remain, nothing is read.</summary>
  [Parameter]
  public bool Compacted { get; set; }

  #endregion

  #region Injected services

  [Inject]
  private UiRequestRunner Runner { get; set; } = null!;

  [Inject]
  private IStringLocalizer<Common> L { get; set; } = null!;

  #endregion

  #region Properties

  private PageState State { get; set; } = PageState.Loading;

  private FileTreeViewModel? Tree { get; set; }

  private string? Flag { get; set; }

  private string? Search { get; set; }

  private bool Searching => !string.IsNullOrWhiteSpace(Search);

  private int MatchCount { get; set; }

  private HashSet<string> Expanded { get; } = new(StringComparer.Ordinal);

  // Null for path order, then ascending, then descending coverage.
  private bool? Sort { get; set; }

  private string AriaSort => Sort switch
  {
    true => "ascending",
    false => "descending",
    var _ => "none",
  };

  private IReadOnlyList<CcSelectOption> FlagOptions =>
    [new CcSelectOption(string.Empty, L["Filter_AllFlags"]), .. Flags.Select(static f => new CcSelectOption(f, f))];

  private IReadOnlyList<(FileNodeViewModel Node, int Depth)> Rows { get; set; } = [];

  #endregion

  #region Lifecycle

  /// <inheritdoc />
  protected override async Task OnParametersSetAsync()
  {
    var key = $"{Forge}/{Owner}/{Name}/{Sha}";

    if (string.Equals(_loaded?.Split('#')[0], key, StringComparison.Ordinal))
    {
      return;
    }

    Flag = null;
    Search = null;
    Expanded.Clear();
    await LoadAsync().ConfigureAwait(true);
  }

  #endregion

  #region Methods

  /// <summary>The address of the annotated source of a file: each segment escaped, so <c>#</c> or a space stays in the path.</summary>
  /// <param name="root">The address of the repository.</param>
  /// <param name="sha">The full SHA of the commit.</param>
  /// <param name="path">The path of the file in the report.</param>
  /// <returns>The relative address.</returns>
  internal static string SourceHref(string root, string sha, string path) =>
    $"{root}/commit/{Uri.EscapeDataString(sha)}/blob/{string.Join('/', path.Split('/').Select(Uri.EscapeDataString))}";

  private string SourceHref(string path) =>
    SourceHref($"{Uri.EscapeDataString(Forge)}/{Uri.EscapeDataString(Owner)}/{Uri.EscapeDataString(Name)}", Sha, path);

  private async Task LoadAsync()
  {
    var (forge, owner, name, sha, flag, format) = (Forge, Owner, Name, Sha, Flag, Format);
    _loaded = $"{forge}/{owner}/{name}/{sha}#{flag}";
    var ticket = _latest.Begin();

    // A compacted commit keeps no index: asking would only be refused (Commit.Compacted).
    if (Compacted)
    {
      Tree = null;
      return;
    }

    State = PageState.Loading;
    var result = await Runner.RunAsync((sender, ct) => sender.GetCommitFilesViewModelAsync(forge, owner, name, sha, flag, format, ct)).ConfigureAwait(true);

    // Another flag or commit was chosen since: its own answer shows it.
    if (!_latest.IsLatest(ticket))
    {
      return;
    }

    (State, Tree) = result switch
    {
      { Succeeded: true, Value: { } tree } => (PageState.Ready, tree),
      { Succeeded: true } => (PageState.Ready, FileTreeViewModel.Empty),
      var _ => (PageState.Failed, null),
    };
    BuildRows();
  }

  private async Task FlagChanged(string? flag)
  {
    Flag = string.IsNullOrEmpty(flag) ? null : flag;
    Expanded.Clear();
    await LoadAsync().ConfigureAwait(true);
  }

  private void SearchChanged(string? search)
  {
    Search = search;
    BuildRows();
  }

  private void Toggle(string path)
  {
    if (!Expanded.Remove(path))
    {
      Expanded.Add(path);
    }

    BuildRows();
  }

  private void ToggleSort()
  {
    Sort = Sort switch
    {
      null => true,
      true => false,
      false => null,
    };
    BuildRows();
  }

  // The rows shown: the matching files flat while searching, else the expanded part of the tree.
  private void BuildRows()
  {
    if (Tree is null)
    {
      Rows = [];
      return;
    }

    if (Searching)
    {
      var term = Search!.Trim();
      var matches = Tree.Files.Where(f => f.Path.Contains(term, StringComparison.OrdinalIgnoreCase)).ToList();
      MatchCount = matches.Count;
      Rows = [.. Ordered(matches).Take(MaxSearchRows).Select(static n => (n, 0))];
      return;
    }

    var rows = new List<(FileNodeViewModel, int)>();
    Walk(Tree.Roots, 0, rows);
    Rows = rows;
  }

  private void Walk(IReadOnlyList<FileNodeViewModel> nodes, int depth, List<(FileNodeViewModel, int)> rows)
  {
    foreach (var node in Ordered(nodes))
    {
      rows.Add((node, depth));

      if (node.IsFolder && Expanded.Contains(node.Path))
      {
        Walk(node.Children, depth + 1, rows);
      }
    }
  }

  // Within a level, coverage order when asked, a node without line last, the ordinal path breaking ties.
  private IEnumerable<FileNodeViewModel> Ordered(IEnumerable<FileNodeViewModel> nodes) => Sort switch
  {
    true => nodes.OrderBy(static n => n.Ratio is null).ThenBy(static n => n.Ratio).ThenBy(static n => n.Path, StringComparer.Ordinal),
    false => nodes.OrderBy(static n => n.Ratio is null).ThenByDescending(static n => n.Ratio).ThenBy(static n => n.Path, StringComparer.Ordinal),
    var _ => nodes.OrderBy(static n => n.Path, StringComparer.Ordinal),
  };

  #endregion
}
