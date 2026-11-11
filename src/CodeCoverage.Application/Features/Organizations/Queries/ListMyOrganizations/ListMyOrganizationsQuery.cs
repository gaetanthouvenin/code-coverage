// ------------------------------------------------------------------------------------------------
// <copyright file="ListMyOrganizationsQuery.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using CodeCoverage.Application.Security;
using CodeCoverage.Domain.Organizations;

namespace CodeCoverage.Application.Features.Organizations.Queries.ListMyOrganizations;

/// <summary>Lists the organizations the signed-in user is linked to, with its role in each.</summary>
/// <remarks>It names no organization: it is what tells a user which ones it may enter.</remarks>
public sealed record ListMyOrganizationsQuery : IQuery<IReadOnlyList<MemberOrganization>>, IUserScoped;
