// ------------------------------------------------------------------------------------------------
// <copyright file="OrganizationSettingResponse.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Api.Contracts.Organizations;

/// <summary>A setting of an organization; none is secret.</summary>
/// <param name="Key">The key of the setting.</param>
/// <param name="Type">The type of its value.</param>
/// <param name="Value">Its effective value, or <see langword="null" /> when the catalog gives none.</param>
/// <param name="Default">The default of the catalog, or <see langword="null" />.</param>
/// <param name="Source">Where the value comes from: the organization (<c>Store</c>) or the catalog (<c>Default</c>).</param>
/// <param name="UpdatedAt">When the organization last changed it, or <see langword="null" />.</param>
/// <param name="UpdatedBy">Who last changed it, or <see langword="null" />.</param>
public sealed record OrganizationSettingResponse(string Key, string Type, string? Value, string? Default, string Source, DateTimeOffset? UpdatedAt, string? UpdatedBy);
