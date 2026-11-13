// ------------------------------------------------------------------------------------------------
// <copyright file="OrganizationRepository.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Domain.Repositories;

/// <summary>A repository of an organization, as the page of its administrators lists it.</summary>
/// <param name="Repository">The repository, with the moment an administrator disabled it.</param>
/// <param name="LastUploadAt">When its last upload was received, or <see langword="null" /> when it never received one.</param>
public sealed record OrganizationRepository(TrackedRepository Repository, DateTimeOffset? LastUploadAt);
