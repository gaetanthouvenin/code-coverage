// ------------------------------------------------------------------------------------------------
// <copyright file="BadgeResponse.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Api.Contracts.Badges;

/// <summary>What a badge shows.</summary>
/// <param name="Label">The text of its left part: the label of the instance, or the flag.</param>
/// <param name="Message">The text of its right part: the coverage, e.g. <c>85%</c>, or <c>unknown</c>.</param>
/// <param name="Color">The color of its right part, e.g. <c>#4c1</c>.</param>
/// <param name="Text">
///   The text of the TXT badge: the coverage alone, e.g. <c>85</c>, as the <c>badge.txt</c> of Codecov, or
///   <c>unknown</c>.
/// </param>
public sealed record BadgeResponse(string Label, string Message, string Color, string Text);
