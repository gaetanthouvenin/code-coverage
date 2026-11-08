// ------------------------------------------------------------------------------------------------
// <copyright file="CcNavItem.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.AspNetCore.Components.Routing;

using CodeCoverage.Web.Components.Ui.Icons;

namespace CodeCoverage.Web.Components.Ui.Models;

/// <summary>A link of the <see cref="CcTopNav" /> or of the <see cref="CcPillTabs" />.</summary>
/// <param name="Href">The address of the link, relative to the base address (<c>admin/settings</c>).</param>
/// <param name="Label">The localized text of the link.</param>
/// <param name="Icon">The icon shown before the text, if any.</param>
/// <param name="Match">
///   How the current address activates the link: <see cref="NavLinkMatch.Prefix" /> (the default)
///   also activates it on the pages below its address.
/// </param>
public sealed record CcNavItem(string Href,
                               string Label,
                               CcIconName? Icon = null,
                               NavLinkMatch Match = NavLinkMatch.Prefix);
