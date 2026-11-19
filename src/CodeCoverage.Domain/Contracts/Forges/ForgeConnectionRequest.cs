// ------------------------------------------------------------------------------------------------
// <copyright file="ForgeConnectionRequest.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Connections;
using CodeCoverage.Domain.Organizations;

namespace CodeCoverage.Domain.Contracts.Forges;

/// <summary>What an administrator gives to connect an account of a forge other than GitHub, before it is verified.</summary>
/// <param name="Forge">The forge of the account, never GitHub.</param>
/// <param name="Kind">The kind of credential the connection holds.</param>
/// <param name="Login">The login of the account on its forge, such as the name of an Azure DevOps organization.</param>
/// <param name="TenantId">The directory tenant that issues the credential, or <see langword="null" /> when the forge has none.</param>
/// <param name="Credential">
///   The credential in clear, which the verifier must not keep, or <see langword="null" /> when the application of the
///   instance acts for every account of the forge, as the Entra application does on Azure DevOps.
/// </param>
public sealed record ForgeConnectionRequest(Forge Forge, ForgeConnectionKind Kind, string Login, Guid? TenantId, string? Credential)
{
  #region Methods

  /// <inheritdoc />
  /// <remarks>Never the credential: an instance can reach a log.</remarks>
  public override string ToString() => $"{nameof(ForgeConnectionRequest)} {{ Forge = {Forge}, Kind = {Kind}, Login = {Login}, TenantId = {TenantId} }}";

  #endregion
}
