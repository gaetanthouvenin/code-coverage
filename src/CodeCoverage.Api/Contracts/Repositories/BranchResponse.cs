// ------------------------------------------------------------------------------------------------
// <copyright file="BranchResponse.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Api.Contracts.Repositories;

/// <summary>A branch of a repository.</summary>
/// <param name="Name">The name of the branch.</param>
/// <param name="HeadSha">The SHA of its head.</param>
/// <param name="HeadUpdatedAt">When its head last moved, in UTC.</param>
/// <param name="CoveredAt">When its covered head was last set, in UTC, or <see langword="null" /> while none is covered.</param>
public sealed record BranchResponse(string Name, string HeadSha, DateTimeOffset HeadUpdatedAt, DateTimeOffset? CoveredAt);
