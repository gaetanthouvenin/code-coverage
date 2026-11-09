// ------------------------------------------------------------------------------------------------
// <copyright file="UploadLogModel.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Api.Contracts.Uploads;

/// <summary>What the processing of an upload reported.</summary>
/// <param name="Messages">The first messages: the formats that are not supported, for instance.</param>
/// <param name="MessageTotal">The number of messages reported.</param>
/// <param name="Paths">The first paths that could not be resolved, or were ambiguous.</param>
/// <param name="PathTotal">The number of such paths.</param>
public sealed record UploadLogModel(IReadOnlyList<string> Messages, int MessageTotal, IReadOnlyList<string> Paths, int PathTotal);
