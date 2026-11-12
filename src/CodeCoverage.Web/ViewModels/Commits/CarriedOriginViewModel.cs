// ------------------------------------------------------------------------------------------------
// <copyright file="CarriedOriginViewModel.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Web.ViewModels.Commits;

/// <summary>Where a carried-forward session of a commit comes from.</summary>
/// <param name="Flags">Its flags, separated by commas, or a dash when it has none.</param>
/// <param name="ShortSha">The seven first characters of the commit its coverage was first carried from.</param>
public sealed record CarriedOriginViewModel(string Flags, string ShortSha);
