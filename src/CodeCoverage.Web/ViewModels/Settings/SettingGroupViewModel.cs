// ------------------------------------------------------------------------------------------------
// <copyright file="SettingGroupViewModel.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Web.ViewModels.Settings;

/// <summary>The settings of a group, in the order of the catalog.</summary>
/// <param name="Group">The name of the group in the catalog, e.g. <c>Uploads</c>; its title is the resource <c>SettingGroup_{Group}</c>.</param>
/// <param name="Settings">Its settings.</param>
public sealed record SettingGroupViewModel(string Group, IReadOnlyList<SettingViewModel> Settings);
