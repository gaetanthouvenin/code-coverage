// ------------------------------------------------------------------------------------------------
// <copyright file="AzureDevOpsProject.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Infrastructure.AzureDevOps.Models;

/// <summary>The project of a repository, as Azure DevOps reports it within the repository.</summary>
/// <param name="Id">The identifier of the project.</param>
/// <param name="Name">The name of the project, which may hold spaces.</param>
/// <param name="Visibility">
///   The visibility of the project, <c>private</c> or <c>public</c>, or <see langword="null" /> when it is not reported.
/// </param>
internal sealed record AzureDevOpsProject(Guid Id, string Name, string? Visibility = null);
