// ------------------------------------------------------------------------------------------------
// <copyright file="UploadCompletion.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Domain.Commits;

/// <summary>Where the uploads of a commit stand when their end is announced.</summary>
/// <param name="Total">How many uploads the commit received.</param>
/// <param name="Processed">How many were processed.</param>
/// <param name="Processing">How many are still received or being processed.</param>
/// <param name="Failed">How many failed.</param>
public sealed record UploadCompletion(int Total, int Processed, int Processing, int Failed);