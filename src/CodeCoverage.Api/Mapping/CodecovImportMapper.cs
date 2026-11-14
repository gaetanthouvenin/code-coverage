// ------------------------------------------------------------------------------------------------
// <copyright file="CodecovImportMapper.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using CodeCoverage.Api.Contracts.Repositories;
using CodeCoverage.Application.Features.Repositories.Commands.StartCodecovImport;

namespace CodeCoverage.Api.Mapping;

/// <summary>Sends the import of the history of Codecov from the API.</summary>
internal static class CodecovImportMapper
{
  #region Methods

  /// <summary>Starts the import of the history of a repository from Codecov.</summary>
  /// <param name="sender">The request sender.</param>
  /// <param name="forge">The forge segment of the address.</param>
  /// <param name="owner">The owner segment of the address.</param>
  /// <param name="repo">The name of the repository.</param>
  /// <param name="request">The API token of Codecov.</param>
  /// <param name="cancellationToken">A token to cancel the request.</param>
  /// <returns>A task that completes when the import is queued.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="sender" /> or <paramref name="request" /> is <see langword="null" />.</exception>
  public static async Task StartCodecovImportAsync(
    this ISender sender,
    string forge,
    string owner,
    string repo,
    StartCodecovImportRequest request,
    CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(sender);
    ArgumentNullException.ThrowIfNull(request);

    await sender.Send(new StartCodecovImportCommand(AddressSegments.ToRepositoryForge(forge), owner, repo, request.Token), cancellationToken).ConfigureAwait(false);
  }

  #endregion
}
