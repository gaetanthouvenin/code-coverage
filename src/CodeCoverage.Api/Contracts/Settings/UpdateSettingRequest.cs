// ------------------------------------------------------------------------------------------------
// <copyright file="UpdateSettingRequest.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Api.Contracts.Settings;

/// <summary>The new value of a setting of the instance.</summary>
/// <param name="Value">The value, checked against the type and the bounds of the setting.</param>
public sealed record UpdateSettingRequest(string Value);
