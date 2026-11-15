// ------------------------------------------------------------------------------------------------
// <copyright file="DocumentHeading.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Web.Documentation;

/// <summary>A heading of a rendered document.</summary>
/// <param name="Id">The identifier of the heading, the anchor of its address (GitHub style).</param>
/// <param name="Text">The text of the heading.</param>
/// <param name="Level">The level of the heading, 2 to 6.</param>
public sealed record DocumentHeading(string Id, string Text, int Level);
