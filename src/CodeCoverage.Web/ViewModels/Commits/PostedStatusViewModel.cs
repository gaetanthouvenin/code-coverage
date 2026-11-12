// ------------------------------------------------------------------------------------------------
// <copyright file="PostedStatusViewModel.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Web.ViewModels.Commits;

/// <summary>A status posted on the forge for a commit.</summary>
/// <param name="Context">Its name, e.g. <c>coverage/project</c>.</param>
/// <param name="State">Its state.</param>
/// <param name="Description">The text posted with it.</param>
/// <param name="TargetUrl">The address it points to when it is an http or https one, else <see langword="null" />.</param>
public sealed record PostedStatusViewModel(string Context, StatusTone State, string Description, string? TargetUrl);
