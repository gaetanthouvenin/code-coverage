// ------------------------------------------------------------------------------------------------
// <copyright file="ReceivedUpload.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Commits;

namespace CodeCoverage.Domain.Uploads;

/// <summary>The outcome of the reception of an upload.</summary>
/// <param name="Upload">The upload.</param>
/// <param name="IsReplay">A value indicating whether the same upload had already been received.</param>
public sealed record ReceivedUpload(Upload Upload, bool IsReplay);
