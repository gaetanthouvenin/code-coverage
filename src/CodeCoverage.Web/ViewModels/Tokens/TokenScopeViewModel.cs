// ------------------------------------------------------------------------------------------------
// <copyright file="TokenScopeViewModel.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Web.ViewModels.Tokens;

/// <summary>An organization a token of the API may be scoped to.</summary>
/// <param name="Forge">The forge segment of the organization, e.g. <c>github</c>.</param>
/// <param name="Login">The login of the organization.</param>
public sealed record TokenScopeViewModel(string Forge, string Login);
