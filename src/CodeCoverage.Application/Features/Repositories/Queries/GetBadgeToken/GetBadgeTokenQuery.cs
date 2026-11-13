// ------------------------------------------------------------------------------------------------
// <copyright file="GetBadgeTokenQuery.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using CodeCoverage.Application.Security;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Users;

namespace CodeCoverage.Application.Features.Repositories.Queries.GetBadgeToken;

/// <summary>Reads the badge token of a repository, the one its README snippet carries.</summary>
/// <param name="Forge">The forge of the repository.</param>
/// <param name="Owner">The owner segment of the address.</param>
/// <param name="Name">The name of the repository.</param>
/// <remarks>
///   Every reader of the repository reads it: the badge of a private repository is unreadable without it, and anyone
///   who can read the repository can already read its coverage. A public repository has no token to give, and the
///   answer is then <see langword="null" />. The value never reaches a log: it is returned, never written down.
/// </remarks>
public sealed record GetBadgeTokenQuery(Forge Forge, string Owner, string Name) : IQuery<string?>, IRepositoryScoped
{
  #region Properties

  /// <inheritdoc />
  public RepositoryPermission RequiredPermission => RepositoryPermission.Read;

  #endregion
}
