// ------------------------------------------------------------------------------------------------
// <copyright file="GetBadgeQuery.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using CodeCoverage.Application.Security;
using CodeCoverage.Domain.Badges;
using CodeCoverage.Domain.Organizations;

namespace CodeCoverage.Application.Features.Repositories.Queries.GetBadge;

/// <summary>Reads the coverage badge of a branch of a repository, public or with its badge token.</summary>
/// <param name="Forge">The forge of the repository.</param>
/// <param name="Owner">The owner segment of the address.</param>
/// <param name="Name">The name of the repository.</param>
/// <param name="Branch">The branch, or <see langword="null" /> for the default branch.</param>
/// <param name="Flag">The flag, case-sensitive, or <see langword="null" /> for the whole repository.</param>
/// <param name="Token">The badge token of a private repository, or <see langword="null" />.</param>
/// <remarks>
///   Anonymous, addressed by its owner (<see cref="IPublicOwnerAddressed" />). It never refuses: anything unknown or
///   refused gives the grey « unknown » badge, never a 404 or a 403, so that nothing is revealed (ARCHITECTURE §11.2).
/// </remarks>
public sealed record GetBadgeQuery(Forge Forge, string Owner, string Name, string? Branch, string? Flag, string? Token) : IQuery<Badge>, IPublicOwnerAddressed
{
  #region Methods

  /// <inheritdoc />
  /// <remarks>Never the token: an instance can reach a log.</remarks>
  public override string ToString() => $"{nameof(GetBadgeQuery)} {{ Owner = {Owner}, Name = {Name}, Branch = {Branch}, Flag = {Flag} }}";

  #endregion
}
