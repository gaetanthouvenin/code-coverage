// ------------------------------------------------------------------------------------------------
// <copyright file="AzureDevOpsGitUser.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Infrastructure.AzureDevOps.Models;

/// <summary>The author or the committer of a commit, as git records them.</summary>
/// <param name="Name">The name of the person, or <see langword="null" />.</param>
/// <param name="Email">The address of the person, or <see langword="null" />.</param>
/// <param name="Date">When the person wrote or committed the change, or <see langword="null" />.</param>
internal sealed record AzureDevOpsGitUser(string? Name = null, string? Email = null, DateTimeOffset? Date = null);
