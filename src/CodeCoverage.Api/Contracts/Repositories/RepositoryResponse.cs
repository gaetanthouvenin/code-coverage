// ------------------------------------------------------------------------------------------------
// <copyright file="RepositoryResponse.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Api.Contracts.Repositories;

/// <summary>A tracked repository.</summary>
/// <param name="Forge">The forge segment of its address, e.g. <c>github</c>.</param>
/// <param name="Owner">Its owner, as the forge names it.</param>
/// <param name="Name">Its name.</param>
/// <param name="DefaultBranch">Its default branch.</param>
/// <param name="IsPrivate">Whether it is private on its forge.</param>
/// <param name="Active">
///   Whether its coverage is processed; <see langword="false" /> once an administrator disabled it: uploads are then
///   refused, while its history, its API and its badge stay readable.
/// </param>
public sealed record RepositoryResponse(string Forge, string Owner, string Name, string DefaultBranch, bool IsPrivate, bool Active);
