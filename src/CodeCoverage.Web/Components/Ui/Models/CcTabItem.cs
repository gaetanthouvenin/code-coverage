// ------------------------------------------------------------------------------------------------
// <copyright file="CcTabItem.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Web.Components.Ui.Models;

/// <summary>A tab of a <see cref="CcFolderTabs" />.</summary>
/// <param name="Id">The identifier of the tab, bound to <see cref="CcFolderTabs.Value" />.</param>
/// <param name="Label">The localized text of the tab.</param>
public sealed record CcTabItem(string Id, string Label);
