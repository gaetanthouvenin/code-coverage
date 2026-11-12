// ------------------------------------------------------------------------------------------------
// <copyright file="OrganizationOverview.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Domain.Organizations;

/// <summary>An organization of the instance and the depth of its queue, as its administrators see it.</summary>
/// <param name="Organization">The row of the organization: identity, state, admission. Nothing of its data.</param>
/// <param name="QueueDepth">The jobs of the organization queued or running.</param>
/// <remarks>A read model of the lifecycle of the organizations: no repository, token, configuration or report.</remarks>
public sealed record OrganizationOverview(Organization Organization, int QueueDepth);
