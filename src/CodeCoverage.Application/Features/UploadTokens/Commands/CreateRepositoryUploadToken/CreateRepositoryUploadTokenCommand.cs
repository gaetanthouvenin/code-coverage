// ------------------------------------------------------------------------------------------------
// <copyright file="CreateRepositoryUploadTokenCommand.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using CodeCoverage.Application.Security;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Tokens;
using CodeCoverage.Domain.Users;

namespace CodeCoverage.Application.Features.UploadTokens.Commands.CreateRepositoryUploadToken;

/// <summary>Creates an upload token for one repository.</summary>
/// <param name="Forge">The forge of the repository.</param>
/// <param name="Owner">The owner of the repository.</param>
/// <param name="Name">The name of the repository.</param>
/// <param name="Label">The name given to the token.</param>
/// <param name="ImportedValue">The value of a <c>CODECOV_TOKEN</c> to import, or <see langword="null" /> to generate one.</param>
/// <param name="ExpiresAt">When it expires, or <see langword="null" />.</param>
public sealed record CreateRepositoryUploadTokenCommand(
  Forge Forge,
  string Owner,
  string Name,
  string Label,
  string? ImportedValue,
  DateTimeOffset? ExpiresAt) : ICommand<IssuedToken>, IRepositoryScoped
{
  #region Properties

  /// <inheritdoc />
  public RepositoryPermission RequiredPermission => RepositoryPermission.Admin;

  #endregion

  #region Methods

  /// <inheritdoc />
  /// <remarks>Never the imported value: an instance can reach a log.</remarks>
  public override string ToString() => $"{nameof(CreateRepositoryUploadTokenCommand)} {{ Owner = {Owner}, Name = {Name} }}";

  #endregion
}
