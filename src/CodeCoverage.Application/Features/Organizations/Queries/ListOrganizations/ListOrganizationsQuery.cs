// ------------------------------------------------------------------------------------------------
// <copyright file="ListOrganizationsQuery.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using CodeCoverage.Application.Security;
using CodeCoverage.Domain.Organizations;

namespace CodeCoverage.Application.Features.Organizations.Queries.ListOrganizations;

/// <summary>Lists the organizations of the instance, pending and suspended ones included, with the depth of their queue.</summary>
/// <remarks>
///   Reserved to the administrators of the instance, with no organization entered: only the rows of
///   <c>Organizations</c> and a count of jobs, never the data of an organization. The rows a renaming tombstoned are
///   left out.
/// </remarks>
public sealed record ListOrganizationsQuery : IQuery<IReadOnlyList<OrganizationOverview>>, IAdminOnly;
