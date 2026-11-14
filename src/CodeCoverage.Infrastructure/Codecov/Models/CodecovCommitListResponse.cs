// ------------------------------------------------------------------------------------------------
// <copyright file="CodecovCommitListResponse.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Infrastructure.Codecov.Models;

/// <summary>A page of <c>GET /api/v2/{service}/{owner}/repos/{repo}/commits/</c>, as the documentation of Codecov describes it.</summary>
/// <param name="Results">The commits of the page.</param>
/// <param name="Next">The address of the next page, or <see langword="null" /> on the last one.</param>
internal sealed record CodecovCommitListResponse(IReadOnlyList<CodecovCommitModel> Results, string? Next = null);
