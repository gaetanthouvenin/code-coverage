// ------------------------------------------------------------------------------------------------
// <copyright file="StartCodecovImportCommand.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using CodeCoverage.Application.Security;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Users;

namespace CodeCoverage.Application.Features.Repositories.Commands.StartCodecovImport;

/// <summary>
///   Starts the import of the history of a repository from the API v2 of Codecov (M5.06): the token is protected and
///   kept until the import ends, and the import runs in a job.
/// </summary>
/// <param name="Forge">The forge of the repository.</param>
/// <param name="Owner">The owner segment of the address.</param>
/// <param name="Name">The name of the repository.</param>
/// <param name="Token">The API token of Codecov, which reads the repository on Codecov.</param>
/// <remarks>
///   The use case is scoped to the repository, as its route: an administrator of the repository (or of its organization)
///   starts it. Nothing is read from Codecov in the request (rule 3).
/// </remarks>
public sealed record StartCodecovImportCommand(Forge Forge, string Owner, string Name, string Token) : ICommand, IRepositoryScoped
{
  #region Properties

  /// <inheritdoc />
  public RepositoryPermission RequiredPermission => RepositoryPermission.Admin;

  #endregion

  #region Methods

  /// <inheritdoc />
  /// <remarks>Never the token: an instance can reach a log.</remarks>
  public override string ToString() => $"{nameof(StartCodecovImportCommand)} {{ Owner = {Owner}, Name = {Name} }}";

  #endregion
}
