// ------------------------------------------------------------------------------------------------
// <copyright file="UploadRequeue.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Commits;

namespace CodeCoverage.Domain.Contracts.Uploads;

/// <summary>An ended upload to process again from its raw files.</summary>
/// <param name="Upload">The upload, in the state it was read in: it is only taken while it is still in that state.</param>
/// <param name="StartingLog">The log its next processing starts from: what must survive the previous one.</param>
public sealed record UploadRequeue(Upload Upload, ProcessingLog StartingLog);
