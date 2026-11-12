// ------------------------------------------------------------------------------------------------
// <copyright file="UpdateOrganizationSettingRequest.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Api.Contracts.Organizations;

/// <summary>The new value of a setting of an organization.</summary>
/// <param name="Value">The value, checked against the type and the bounds of the setting.</param>
public sealed record UpdateOrganizationSettingRequest(string Value);
