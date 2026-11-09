// ------------------------------------------------------------------------------------------------
// <copyright file="CiContext.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Domain.Contracts.Uploads;

/// <summary>Where an upload was sent from.</summary>
/// <param name="Service">The CI service, or <see langword="null" />.</param>
/// <param name="BuildUrl">The address of the build, or <see langword="null" />.</param>
/// <param name="BuildId">The identifier of the build, or <see langword="null" />.</param>
/// <param name="JobId">The identifier of the job, or <see langword="null" />.</param>
public sealed record CiContext(string? Service, string? BuildUrl, string? BuildId, string? JobId);
