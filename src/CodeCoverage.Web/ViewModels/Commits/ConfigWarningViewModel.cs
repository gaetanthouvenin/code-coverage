// ------------------------------------------------------------------------------------------------
// <copyright file="ConfigWarningViewModel.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Web.ViewModels.Commits;

/// <summary>A warning raised while the configuration of a commit was read.</summary>
/// <param name="Code">Its code, a key of the resources (e.g. <c>Config.InvalidValue</c>).</param>
/// <param name="Key">The YAML key it concerns.</param>
/// <param name="Detail">The value or pattern at fault, or <see langword="null" />.</param>
public sealed record ConfigWarningViewModel(string Code, string Key, string? Detail);
