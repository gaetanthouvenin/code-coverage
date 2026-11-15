// ------------------------------------------------------------------------------------------------
// <copyright file="ConsoleLanguage.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Web.Localization;

/// <summary>A language of the console.</summary>
/// <param name="Code">The two-letter code of the neutral culture, for example <c>it</c>.</param>
/// <param name="NativeName">The name of the language in itself, for example <c>Italiano</c>, never translated.</param>
public sealed record ConsoleLanguage(string Code, string NativeName);
