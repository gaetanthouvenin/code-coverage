// ------------------------------------------------------------------------------------------------
// <copyright file="OrganizationRepositoryViewModel.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Web.ViewModels.Repositories;

/// <summary>A repository of an organization, as the page of its administrators lists it.</summary>
/// <param name="Name">The name of the repository on its forge.</param>
/// <param name="IsActive">Whether it accepts uploads and publishes; false once an administrator disabled it.</param>
/// <param name="LastUploadAt">When its last upload was received, or <see langword="null" /> when it never received one.</param>
public sealed record OrganizationRepositoryViewModel(string Name, bool IsActive, DateTimeOffset? LastUploadAt);
