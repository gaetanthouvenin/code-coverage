// ------------------------------------------------------------------------------------------------
// <copyright file="OrganizationResponse.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Api.Contracts.Organizations;

/// <summary>An active organization the user belongs to.</summary>
/// <param name="Forge">The forge segment of its address, e.g. <c>github</c>.</param>
/// <param name="Login">Its login.</param>
/// <param name="Role">The role of the user in it: participant or administrator.</param>
public sealed record OrganizationResponse(string Forge, string Login, string Role);
