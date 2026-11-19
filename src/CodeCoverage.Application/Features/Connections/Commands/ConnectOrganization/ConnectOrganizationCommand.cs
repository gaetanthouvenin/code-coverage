// ------------------------------------------------------------------------------------------------
// <copyright file="ConnectOrganizationCommand.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using CodeCoverage.Application.Security;
using CodeCoverage.Domain.Connections;
using CodeCoverage.Domain.Organizations;

namespace CodeCoverage.Application.Features.Connections.Commands.ConnectOrganization;

/// <summary>
///   Admits an account of a forge other than GitHub by its connection: the forge verifies the access, then the
///   organization, its connection and the jobs that follow them are saved together (ADR-017).
/// </summary>
/// <param name="Forge">The forge of the account, never GitHub, which admits by its installations.</param>
/// <param name="Login">The login of the account on its forge, such as the name of an Azure DevOps organization.</param>
/// <param name="Kind">The kind of credential the connection holds.</param>
/// <param name="TenantId">The directory tenant that issues the credential, or <see langword="null" /> when the forge has none.</param>
/// <param name="Credential">
///   The credential in clear, protected before it is stored, or <see langword="null" /> when the application of the
///   instance acts for every account of the forge, as the Entra application does on Azure DevOps.
/// </param>
/// <remarks>
///   The call to the forge is a light verification; the synchronization of the repositories stays a job. The same login
///   on two forges gives two organizations.
/// </remarks>
public sealed record ConnectOrganizationCommand(Forge Forge, string Login, ForgeConnectionKind Kind, Guid? TenantId, string? Credential)
  : ICommand<Organization>, IAdminOnly
{
  #region Methods

  /// <inheritdoc />
  /// <remarks>Never the credential: an instance can reach a log.</remarks>
  public override string ToString() =>
    $"{nameof(ConnectOrganizationCommand)} {{ Forge = {Forge}, Login = {Login}, Kind = {Kind}, TenantId = {TenantId} }}";

  #endregion
}
