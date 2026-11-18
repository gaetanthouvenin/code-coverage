// ------------------------------------------------------------------------------------------------
// <copyright file="AzureDevOpsRef.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Infrastructure.AzureDevOps.Models;

/// <summary>A git reference, as listed by <c>GET _apis/git/repositories/{repositoryId}/refs</c>.</summary>
/// <param name="Name">The full name of the reference, <c>refs/heads/main</c>.</param>
/// <param name="ObjectId">The SHA of the commit the reference points to.</param>
internal sealed record AzureDevOpsRef(string Name, string ObjectId);
