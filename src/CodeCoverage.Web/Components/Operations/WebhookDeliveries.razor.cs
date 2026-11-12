// ------------------------------------------------------------------------------------------------
// <copyright file="WebhookDeliveries.razor.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;

using CodeCoverage.Web.Resources;
using CodeCoverage.Web.ViewModels.Operations;

namespace CodeCoverage.Web.Components.Operations;

/// <summary>The latest delivery of each GitHub event, with its state; refused deliveries (bad signature) are never stored, so never shown.</summary>
public partial class WebhookDeliveries
{
  #region Parameters

  /// <summary>Gets or sets the latest delivery of each event.</summary>
  [Parameter, EditorRequired]
  public IReadOnlyList<WebhookEventViewModel> Webhooks { get; set; } = null!;

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
