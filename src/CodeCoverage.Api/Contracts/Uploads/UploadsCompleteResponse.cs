// ------------------------------------------------------------------------------------------------
// <copyright file="UploadsCompleteResponse.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Api.Contracts.Uploads;

/// <summary>Where the uploads of a commit stand when their end is announced.</summary>
/// <param name="Total">The number of uploads the commit received.</param>
/// <param name="Processed">The number of uploads that were processed.</param>
/// <param name="Processing">The number of uploads still received or being processed: the commit is notified once they end.</param>
/// <param name="Failed">The number of uploads that failed.</param>
public sealed record UploadsCompleteResponse(int Total, int Processed, int Processing, int Failed);