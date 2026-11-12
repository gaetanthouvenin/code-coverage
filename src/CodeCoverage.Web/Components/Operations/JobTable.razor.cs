// ------------------------------------------------------------------------------------------------
// <copyright file="JobTable.razor.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;

using CodeCoverage.Web.Resources;
using CodeCoverage.Web.ViewModels.Operations;

namespace CodeCoverage.Web.Components.Operations;

/// <summary>The failed and dead jobs, metadata only, each with its retry button.</summary>
public partial class JobTable
{
  #region Parameters

  /// <summary>Gets or sets the jobs.</summary>
  [Parameter, EditorRequired]
  public IReadOnlyList<JobRowViewModel> Jobs { get; set; } = null!;

  /// <summary>Gets or sets the callback that retries a job.</summary>
  [Parameter]
  public EventCallback<JobRowViewModel> OnRetry { get; set; }

  /// <summary>Gets or sets a value indicating whether a retry is being sent.</summary>
  [Parameter]
  public bool Busy { get; set; }

  #endregion

  #region Injected services

  [Inject]
  private IStringLocalizer<Common> L { get; set; } = null!;

  [Inject]
  private TimeProvider Time { get; set; } = null!;

  #endregion

  #region Methods

  private string AgeOf(DateTimeOffset at) => Mapping.DisplayFormatter.Age(at, Time.GetUtcNow(), L);

  #endregion
}
