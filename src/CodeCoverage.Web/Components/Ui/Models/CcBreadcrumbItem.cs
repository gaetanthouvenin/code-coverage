// ------------------------------------------------------------------------------------------------
// <copyright file="CcBreadcrumbItem.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Web.Components.Ui.Models;

/// <summary>A step of the breadcrumb of a <see cref="CcHeader" />.</summary>
/// <param name="Label">The localized text of the step.</param>
/// <param name="Href">The address of the step; <see langword="null" /> for the current page.</param>
public sealed record CcBreadcrumbItem(string Label, string? Href = null);
