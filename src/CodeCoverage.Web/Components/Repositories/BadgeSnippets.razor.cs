// ------------------------------------------------------------------------------------------------
// <copyright file="BadgeSnippets.razor.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;

using CodeCoverage.Web.Components.Ui.Models;
using CodeCoverage.Web.Resources;
using CodeCoverage.Web.ViewModels.Repositories;

namespace CodeCoverage.Web.Components.Repositories;

/// <summary>
///   The badge of a repository: the image as it will be served, and the Markdown and HTML snippets to paste in a
///   README. The snippet of a private repository carries its badge token, which every reader of the repository may
///   read. Without the public addresses of the hosts, no snippet can be written, and the panel says so.
/// </summary>
public partial class BadgeSnippets
{
  #region Fields

  /// <summary>The identifier of the Markdown tab.</summary>
  private const string MarkdownFormat = "md";

  /// <summary>The identifier of the HTML tab.</summary>
  private const string HtmlFormat = "html";

  private string _format = MarkdownFormat;

  #endregion

  #region Parameters

  /// <summary>Gets or sets the snippets, or <see langword="null" /> while the public addresses are unknown.</summary>
  [Parameter]
  public BadgeSnippetsViewModel? Badge { get; set; }

  /// <summary>Gets or sets a value indicating whether the repository is private, which the snippet says.</summary>
  [Parameter]
  public bool IsPrivate { get; set; }

  #endregion

  #region Injected services

  [Inject]
  private IStringLocalizer<Common> L { get; set; } = null!;

  #endregion

  #region Properties

  private IReadOnlyList<CcTabItem> Formats => [new CcTabItem(MarkdownFormat, "Markdown"), new CcTabItem(HtmlFormat, "HTML")];

  #endregion
}
