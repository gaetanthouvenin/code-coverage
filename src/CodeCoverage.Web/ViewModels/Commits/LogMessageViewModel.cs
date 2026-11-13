// ------------------------------------------------------------------------------------------------
// <copyright file="LogMessageViewModel.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Web.ViewModels.Commits;

/// <summary>A message of the processing log of an upload.</summary>
/// <param name="Code">The error code the message starts with (<c>Upload.UnsupportedFormat</c>), or <see langword="null" /> for a free message.</param>
/// <param name="Text">The argument after the code, or the whole message when it has no code.</param>
public sealed record LogMessageViewModel(string? Code, string Text);
