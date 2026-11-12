// ------------------------------------------------------------------------------------------------
// <copyright file="ImportBadgeTokenCommand.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using CodeCoverage.Application.Security;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Users;

namespace CodeCoverage.Application.Features.Repositories.Commands.ImportBadgeToken;

/// <summary>
///   Takes the graphing token of Codecov as the badge token of a repository, so that the URL of a README badge only
///   changes its host.
/// </summary>
/// <param name="Forge">The forge of the repository.</param>
/// <param name="Owner">The owner segment of the address.</param>
/// <param name="Name">The name of the repository.</param>
/// <param name="Value">The token, 1 to 64 characters of <c>[A-Za-z0-9_-]</c>.</param>
public sealed record ImportBadgeTokenCommand(Forge Forge, string Owner, string Name, string Value) : ICommand, IRepositoryScoped
{
  #region Properties

  /// <inheritdoc />
  public RepositoryPermission RequiredPermission => RepositoryPermission.Admin;

  #endregion

  #region Methods

  /// <inheritdoc />
  /// <remarks>Never the value: an instance can reach a log.</remarks>
  public override string ToString() => $"{nameof(ImportBadgeTokenCommand)} {{ Owner = {Owner}, Name = {Name} }}";

  #endregion
}
