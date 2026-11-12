// ------------------------------------------------------------------------------------------------
// <copyright file="CurrentUserViewModel.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Web.ViewModels.Access;

/// <summary>What a page shows of the signed-in user: never a token, never a role.</summary>
/// <param name="Id">The internal identifier of the user.</param>
/// <param name="Login">The login of its account.</param>
/// <param name="AvatarUrl">The address of its avatar, or <see langword="null" />.</param>
public sealed record CurrentUserViewModel(long Id, string Login, string? AvatarUrl);
