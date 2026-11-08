// ------------------------------------------------------------------------------------------------
// <copyright file="PingResponse.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Api.Contracts.Diagnostics;

/// <summary>The answer of the diagnostic ping.</summary>
/// <param name="Version">The version of the service.</param>
public sealed record PingResponse(string Version);
