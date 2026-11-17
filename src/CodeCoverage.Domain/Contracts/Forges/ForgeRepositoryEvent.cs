// ------------------------------------------------------------------------------------------------
// <copyright file="ForgeRepositoryEvent.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Domain.Contracts.Forges;

/// <summary>A change of a repository, as any forge reports it once its delivery is translated.</summary>
/// <param name="Kind">What happened to the repository.</param>
/// <param name="Repository">
///   The repository as the delivery describes it after the change; its <see cref="ForgeRepositoryInfo.OwnerExternalId" />
///   names the account that owns it now.
/// </param>
/// <param name="PreviousOwnerExternalId">
///   The identifier of the account that owned the repository before a transfer, or <see langword="null" /> when the
///   change is not a transfer or the forge does not say.
/// </param>
public sealed record ForgeRepositoryEvent(ForgeRepositoryEventKind Kind, ForgeRepositoryInfo Repository, string? PreviousOwnerExternalId = null);
