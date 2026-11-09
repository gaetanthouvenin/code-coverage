// ------------------------------------------------------------------------------------------------
// <copyright file="ReceiveUploadCommand.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using CodeCoverage.Application.Security;
using CodeCoverage.Domain.Contracts.Uploads;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Uploads;

namespace CodeCoverage.Application.Features.Uploads.Commands.ReceiveUpload;

/// <summary>Stores an upload as it is received and queues its processing, without parsing anything.</summary>
/// <param name="Credential">The credential presented with the upload.</param>
/// <param name="Metadata">What the text fields describe.</param>
/// <param name="Parts">The reports and the list of files, read as they arrive.</param>
/// <param name="IdempotencyKey">The key that recognizes a replay, or <see langword="null" /> to compute it.</param>
public sealed record ReceiveUploadCommand(
  UploadCredential Credential,
  UploadMetadata Metadata,
  IAsyncEnumerable<UploadPart> Parts,
  string? IdempotencyKey) : ICommand<ReceivedUpload>, IPublicOwnerAddressed
{
  #region Properties

  /// <inheritdoc />
  public Forge Forge => Metadata.Forge;

  /// <inheritdoc />
  public string Owner => Metadata.Slug.Owner;

  #endregion
}
