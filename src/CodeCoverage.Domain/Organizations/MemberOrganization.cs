// ------------------------------------------------------------------------------------------------
// <copyright file="MemberOrganization.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Domain.Organizations;

/// <summary>An organization a user is linked to, with the role the user holds there now.</summary>
/// <param name="Organization">The organization, active.</param>
/// <param name="Role">The role of the user in it.</param>
public sealed record MemberOrganization(Organization Organization, OrganizationRole Role);
