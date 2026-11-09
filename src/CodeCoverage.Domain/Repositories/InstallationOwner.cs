// ------------------------------------------------------------------------------------------------
// <copyright file="InstallationOwner.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Organizations;

namespace CodeCoverage.Domain.Repositories;

/// <summary>The organization of an installation, as a webhook needs it to choose its lane.</summary>
/// <param name="OrganizationId">The identifier of the organization.</param>
/// <param name="State">The place of the organization in its lifecycle.</param>
public sealed record InstallationOwner(long OrganizationId, OrganizationState State);
