// ------------------------------------------------------------------------------------------------
// <copyright file="AzureDevOpsCommitRef.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Infrastructure.AzureDevOps.Models;

/// <summary>A commit named by a pull request: its head, the head of its target or its merge commit.</summary>
/// <param name="CommitId">The SHA of the commit.</param>
internal sealed record AzureDevOpsCommitRef(string CommitId);
