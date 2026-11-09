// ------------------------------------------------------------------------------------------------
// <copyright file="UploadAcceptedResponse.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Api.Contracts.Uploads;

/// <summary>The upload that was accepted.</summary>
/// <param name="UploadId">The public identifier of the upload.</param>
/// <param name="State">Where it stands in its processing.</param>
/// <param name="CommitUrl">The page of its commit in the Web console.</param>
/// <param name="StatusUrl">Where to read its state and its processing log.</param>
public sealed record UploadAcceptedResponse(Guid UploadId, string State, string CommitUrl, string StatusUrl);
