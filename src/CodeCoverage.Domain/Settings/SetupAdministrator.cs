// ------------------------------------------------------------------------------------------------
// <copyright file="SetupAdministrator.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Domain.Settings;

/// <summary>An administrator the first-start wizard will name, as the operator typed it and as it is stored.</summary>
/// <param name="UserId">The numeric identifier GitHub gives the account, as text: the only value stored.</param>
/// <param name="Login">The login of the account, as GitHub spells it, or <see langword="null" /> when an identifier was typed.</param>
public sealed record SetupAdministrator(string UserId, string? Login);
