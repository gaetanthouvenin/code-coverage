// ------------------------------------------------------------------------------------------------
// <copyright file="IForgeConnectionVerifier.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Contracts.Forges;

namespace CodeCoverage.Domain.Ports;

/// <summary>Verifies, by a light call to its forge, that a connection reaches the account it names (ADR-017).</summary>
/// <remarks>
///   Each forge other than GitHub implements it under its <see cref="Organizations.Forge" />, which
///   <see cref="IForgeGatewayProvider.GetVerifier" /> resolves. The admission, the rotation of a credential and the
///   daily check of the connections share it; the synchronization of the repositories stays a job.
/// </remarks>
public interface IForgeConnectionVerifier
{
  #region Methods

  /// <summary>Verifies a connection before it is saved or while it is checked.</summary>
  /// <param name="request">The account and the credential to verify; the credential is never kept nor logged.</param>
  /// <param name="cancellationToken">A token to cancel the verification.</param>
  /// <returns>The account the connection reaches, or the refusal of the forge.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="request" /> is <see langword="null" />.</exception>
  /// <exception cref="ArgumentException"><paramref name="request" /> is not a request of the forge of the verifier.</exception>
  Task<ForgeConnectionCheck> VerifyAsync(ForgeConnectionRequest request, CancellationToken cancellationToken);

  #endregion
}
