// ------------------------------------------------------------------------------------------------
// <copyright file="RenderLabels.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Web.Documentation.Rendering;

/// <summary>The localized texts the rendered HTML carries around the content of a document.</summary>
/// <param name="Anchor">The accessible name of the « # » link of a heading, in the language of the console.</param>
/// <param name="Copy">The label of the copy button of a code block, in the language of the console.</param>
/// <param name="Copied">The announcement of a successful copy, in the language of the console.</param>
/// <param name="CopyFailed">The announcement of a copy the browser refused, in the language of the console.</param>
/// <param name="Note">The title of a <c>[!NOTE]</c> or <c>[!IMPORTANT]</c> callout, in the language of the document.</param>
/// <param name="Tip">The title of a <c>[!TIP]</c> callout, in the language of the document.</param>
/// <param name="Warning">The title of a <c>[!WARNING]</c> or <c>[!CAUTION]</c> callout, in the language of the document.</param>
internal sealed record RenderLabels(string Anchor, string Copy, string Copied, string CopyFailed, string Note, string Tip, string Warning);
