// ------------------------------------------------------------------------------------------------
// <copyright file="CcSelectOption.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Web.Components.Ui.Models;

/// <summary>An option of a <see cref="CcSelectField" />.</summary>
/// <param name="Value">The value bound to <see cref="CcSelectField.Value" />.</param>
/// <param name="Label">The localized text of the option.</param>
public sealed record CcSelectOption(string Value, string Label);
