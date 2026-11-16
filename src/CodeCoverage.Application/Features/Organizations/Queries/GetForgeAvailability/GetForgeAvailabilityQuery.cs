// ------------------------------------------------------------------------------------------------
// <copyright file="GetForgeAvailabilityQuery.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using CodeCoverage.Application.Security;
using CodeCoverage.Domain.Organizations;

namespace CodeCoverage.Application.Features.Organizations.Queries.GetForgeAvailability;

/// <summary>Gets the forges of the instance an organization can be admitted on and a user can sign in with.</summary>
/// <remarks>
///   An anonymous entry: the list of the forges protects nothing and serves the sign-in page, before any user is
///   known. It names forges only, nothing of an organization.
/// </remarks>
public sealed record GetForgeAvailabilityQuery : IQuery<ForgeAvailabilitySnapshot>, IAnonymousEntry;
