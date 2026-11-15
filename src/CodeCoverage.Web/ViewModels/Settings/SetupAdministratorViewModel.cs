// ------------------------------------------------------------------------------------------------
// <copyright file="SetupAdministratorViewModel.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Web.ViewModels.Settings;

/// <summary>An administrator the wizard will name, shown as « login → identifier » before the operator confirms.</summary>
/// <param name="UserId">The numeric GitHub identifier, the only value stored.</param>
/// <param name="Login">The login GitHub gave for it, or <see langword="null" /> when the identifier was typed.</param>
public sealed record SetupAdministratorViewModel(string UserId, string? Login);
