// ------------------------------------------------------------------------------------------------
// <copyright file="ImpactedFiles.razor.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;

using CodeCoverage.Web.Resources;
using CodeCoverage.Web.ViewModels.Comparisons;

namespace CodeCoverage.Web.Components.Comparisons;

/// <summary>
///   The files the diff touches, sorted as the comment sorts them (changed lines missed, then the size of the change,
///   then the path) but all listed, 50 a page; each links to its annotated source, a removed one to nothing.
/// </summary>
public partial class ImpactedFiles
{
  #region Fields

  /// <summary>The rows of a page.</summary>
  internal const int PageSize = 50;

  #endregion

  #region Parameters

  /// <summary>Gets or sets the files, already sorted.</summary>
  [Parameter, EditorRequired]
  public IReadOnlyList<ImpactedFileViewModel> Files { get; set; } = null!;

  #endregion

  #region Injected services

  [Inject]
  private IStringLocalizer<Common> L { get; set; } = null!;

  #endregion

  #region Properties

  private int Page { get; set; } = 1;

  private IEnumerable<ImpactedFileViewModel> Shown => Files.Skip((Page - 1) * PageSize).Take(PageSize);

  #endregion

  #region Methods

  private static string DeltaClass(int trend) => trend switch
  {
    > 0 => "cc-delta--up",
    < 0 => "cc-delta--down",
    var _ => "cc-dim",
  };

  #endregion
}
