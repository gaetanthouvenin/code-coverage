// ------------------------------------------------------------------------------------------------
// <copyright file="AccountTokensViewModel.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Web.ViewModels.Tokens;

/// <summary>The personal tokens of the API of the signed-in user, and what a new one may be scoped to.</summary>
/// <param name="Tokens">The tokens, the newest first, the revoked ones included.</param>
/// <param name="Organizations">The organizations the user may scope a token to, by login.</param>
/// <param name="CanScopeToInstance">Whether the instance scope is offered: only to the administrators of the instance.</param>
public sealed record AccountTokensViewModel(
  IReadOnlyList<ApiTokenViewModel> Tokens,
  IReadOnlyList<TokenScopeViewModel> Organizations,
  bool CanScopeToInstance);
