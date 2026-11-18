// ------------------------------------------------------------------------------------------------
// <copyright file="AzureDevOpsList.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Infrastructure.AzureDevOps.Models;

/// <summary>The envelope of a listing of Azure DevOps.</summary>
/// <typeparam name="T">The type of the items.</typeparam>
/// <param name="Count">The number of items of this answer.</param>
/// <param name="Value">The items.</param>
internal sealed record AzureDevOpsList<T>(int Count, IReadOnlyList<T> Value);
