// ------------------------------------------------------------------------------------------------
// <copyright file="QuotaViewModel.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Web.ViewModels.Operations;

/// <summary>The GitHub quota of an installation, or of the App.</summary>
/// <param name="Installation">The installation, or <c>app</c>.</param>
/// <param name="Remaining">The requests left.</param>
/// <param name="Limit">The requests of the window, or <see langword="null" /> when GitHub did not say.</param>
/// <param name="ResetAt">When the window starts again, or <see langword="null" />.</param>
/// <param name="IsExpired">Whether the window already started again since GitHub reported it: the quota is refilled since, its figures unknown.</param>
public sealed record QuotaViewModel(string Installation, long Remaining, long? Limit, DateTimeOffset? ResetAt, bool IsExpired)
{
  #region Properties

  /// <summary>Gets the share of the window left, in percent, or <see langword="null" /> without a limit.</summary>
  public double? RemainingPercent => Limit is > 0 and var limit ? Remaining * 100d / limit : null;

  #endregion
}
