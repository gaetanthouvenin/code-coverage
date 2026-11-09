// ------------------------------------------------------------------------------------------------
// <copyright file="ForgePullFiles.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Domain.Contracts.Forges;

/// <summary>The files changed by a pull request, as its forge lists them.</summary>
/// <param name="Files">The changed files that the forge returned.</param>
/// <param name="Truncated">A value indicating whether the forge may have left out some of the changed files.</param>
public sealed record ForgePullFiles(IReadOnlyList<ForgeChangedFile> Files, bool Truncated);
