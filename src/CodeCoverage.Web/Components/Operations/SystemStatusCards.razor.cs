// ------------------------------------------------------------------------------------------------
// <copyright file="SystemStatusCards.razor.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;

using CodeCoverage.Web.Resources;
using CodeCoverage.Web.ViewModels.Operations;

namespace CodeCoverage.Web.Components.Operations;

/// <summary>The cards of the operations: queue depth and oldest job, dead jobs, GitHub quota and workers, the indicators of §12.6.</summary>
public partial class SystemStatusCards
{
  #region Parameters

  /// <summary>Gets or sets the operations.</summary>
  [Parameter, EditorRequired]
  public OperationsViewModel Status { get; set; } = null!;

  #endregion

  #region Injected services

  [Inject]
  private IStringLocalizer<Common> L { get; set; } = null!;

  [Inject]
  private TimeProvider Time { get; set; } = null!;

  #endregion

  #region Methods

  private string AgeOf(DateTimeOffset at) => Mapping.DisplayFormatter.Age(at, Time.GetUtcNow(), L);

  private string UntilOf(DateTimeOffset at) => Mapping.DisplayFormatter.Until(at, Time.GetUtcNow(), L);

  #endregion
}
