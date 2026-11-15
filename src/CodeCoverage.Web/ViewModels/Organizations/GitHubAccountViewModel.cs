// ------------------------------------------------------------------------------------------------
// <copyright file="GitHubAccountViewModel.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Web.ViewModels.Organizations;

/// <summary>A GitHub account found by its login, which the dialog of admission shows for confirmation.</summary>
/// <param name="Login">The login, as GitHub spells it.</param>
/// <param name="ExternalAccountId">The identifier GitHub gives the account.</param>
/// <param name="IsUser">Whether the account is a personal account rather than an organization.</param>
public sealed record GitHubAccountViewModel(string Login, string ExternalAccountId, bool IsUser);
