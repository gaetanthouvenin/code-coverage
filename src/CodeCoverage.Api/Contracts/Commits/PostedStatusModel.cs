// ------------------------------------------------------------------------------------------------
// <copyright file="PostedStatusModel.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Api.Contracts.Commits;

/// <summary>A commit status posted to the forge.</summary>
/// <param name="Context">The name of the status, e.g. <c>coverage/project</c>.</param>
/// <param name="State">Its state.</param>
/// <param name="Description">Its description.</param>
/// <param name="TargetUrl">The address it links to, or <see langword="null" />.</param>
/// <param name="PostedAt">When it was posted, in UTC.</param>
public sealed record PostedStatusModel(string Context, string State, string Description, string? TargetUrl, DateTimeOffset PostedAt);
