// ------------------------------------------------------------------------------------------------
// <copyright file="Common.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Web.Resources;

/// <summary>
///   The marker of the strings of the application (layout, pages, error codes), read through
///   <c>IStringLocalizer&lt;Common&gt;</c> from <c>Common.resx</c> (English) and
///   <c>Common.fr.resx</c> (French).
/// </summary>
/// <remarks>
///   Every error code <c>Feature.Reason</c> of the Application is translated here, under its own
///   name. The few strings of the UI components stay in <see cref="UiStrings" />.
/// </remarks>
public sealed class Common;
