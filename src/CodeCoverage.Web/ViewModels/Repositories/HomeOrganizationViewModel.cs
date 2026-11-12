// ------------------------------------------------------------------------------------------------
// <copyright file="HomeOrganizationViewModel.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Web.ViewModels.Repositories;

/// <summary>An organization of the user and the repositories of it the user can read, as the home page groups them.</summary>
/// <param name="Login">Its login.</param>
/// <param name="ForgeName">The name of its forge in the resources (<c>Forge_{name}</c>).</param>
/// <param name="Repositories">Its repositories the user can read, by name.</param>
public sealed record HomeOrganizationViewModel(string Login, string ForgeName, IReadOnlyList<RepositorySummaryViewModel> Repositories);
