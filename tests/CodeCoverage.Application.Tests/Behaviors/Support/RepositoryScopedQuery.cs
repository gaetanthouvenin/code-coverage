// ------------------------------------------------------------------------------------------------
// <copyright file="RepositoryScopedQuery.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using CodeCoverage.Application.Security;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Users;

namespace CodeCoverage.Application.Tests.Behaviors.Support;

// A query on one repository, addressed by its forge and its owner.
internal sealed record RepositoryScopedQuery(Forge Forge, string Owner, RepositoryPermission RequiredPermission = RepositoryPermission.Read) : IQuery<string>, IRepositoryScoped
{
  #region Properties

  public string Name => "repository";

  #endregion
}
