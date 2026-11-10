// ------------------------------------------------------------------------------------------------
// <copyright file="ReceiveUploadCommandHandler.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using Mediator;

using Microsoft.Extensions.Options;

using CodeCoverage.Application.Configuration;
using CodeCoverage.Application.Exceptions;
using CodeCoverage.Application.Exceptions.ErrorCodes;
using CodeCoverage.Application.Features.Repositories;
using CodeCoverage.Application.Services;
using CodeCoverage.Domain.Commits;
using CodeCoverage.Domain.Contracts.Forges;
using CodeCoverage.Domain.Contracts.Jobs;
using CodeCoverage.Domain.Contracts.Uploads;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Domain.Repositories;
using CodeCoverage.Domain.Uploads;

namespace CodeCoverage.Application.Features.Uploads.Commands.ReceiveUpload;

/// <summary>Handler for the <see cref="ReceiveUploadCommand" /> command.</summary>
/// <param name="authenticator">Checks the credential again.</param>
/// <param name="repositories">The repositories of the organization.</param>
/// <param name="directory">The directory of the organizations.</param>
/// <param name="installations">The installations of the GitHub App.</param>
/// <param name="gateways">The gateways of the forges.</param>
/// <param name="commits">The commits of the organization.</param>
/// <param name="rawStore">The store of the received parts.</param>
/// <param name="uploads">The uploads of the organization.</param>
/// <param name="reports">The saving of an upload with its job.</param>
/// <param name="tokenGenerator">The generator of the badge token.</param>
/// <param name="secretProtector">The protection of the badge token.</param>
/// <param name="options">The limits of the uploads.</param>
/// <param name="clock">The clock.</param>
internal sealed class ReceiveUploadCommandHandler(
  UploadAuthenticator authenticator,
  IRepositoryRepository repositories,
  IOrganizationDirectory directory,
  IInstallationRepository installations,
  IForgeGatewayProvider gateways,
  ICommitRepository commits,
  IRawUploadStore rawStore,
  IUploadRepository uploads,
  ICommitReportRepository reports,
  ITokenGenerator tokenGenerator,
  ISecretProtector secretProtector,
  IOptionsMonitor<UploadOptions> options,
  IClock clock) : ICommandHandler<ReceiveUploadCommand, ReceivedUpload>
{
  #region Methods

  /// <summary>Builds the refusal of a repository that cannot be reached.</summary>
  /// <returns>The exception.</returns>
  private static NotFoundException RepositoryNotFound() => new(RepositoryErrors.NotFound, "The repository does not exist or cannot be read.");

  /// <summary>Computes the key of an upload sent without one.</summary>
  /// <param name="metadata">What the text fields describe.</param>
  /// <param name="fileHashes">The SHA-256 of the parts.</param>
  /// <returns>The key: <c>sha256(slug|sha|flags|name|buildId|jobId|sha256 of the files)</c>, in hexadecimal.</returns>
  private static string ComputeIdempotencyKey(UploadMetadata metadata, IEnumerable<string> fileHashes)
  {
    string[] fields =
    [
      $"{metadata.Slug.Owner}/{metadata.Slug.Name}",
      metadata.Sha.Value,
      string.Join(',', metadata.Flags.Order(StringComparer.Ordinal)),
      metadata.Name ?? string.Empty,
      metadata.Ci.BuildId ?? string.Empty,
      metadata.Ci.JobId ?? string.Empty,
      string.Join(',', fileHashes.Order(StringComparer.Ordinal)),
    ];

    return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('|', fields))));
  }

  /// <summary>Stores the parts of an upload as they arrive.</summary>
  /// <param name="publicId">The public identifier the upload will have.</param>
  /// <param name="parts">The parts.</param>
  /// <param name="cancellationToken">A token to cancel the saving.</param>
  /// <returns>The SHA-256 of the parts, in hexadecimal.</returns>
  /// <exception cref="PayloadTooLargeException">The content exceeds a limit.</exception>
  /// <exception cref="BadRequestException">
  ///   The upload holds no report, or too many; a part has a name too long or a corrupted gzip; or the upload holds
  ///   more than one network part.
  /// </exception>
  private async Task<IReadOnlyList<string>> StoreAsync(Guid publicId, IAsyncEnumerable<UploadPart> parts, CancellationToken cancellationToken)
  {
    var maxReports = options.CurrentValue.MaxFilesPerUpload;
    var hashes = new List<string>();
    var reportCount = 0;
    var networkCount = 0;

    try
    {
      await foreach (var part in parts.WithCancellation(cancellationToken).ConfigureAwait(false))
      {
        if (part.Kind is UploadPartKind.Report && ++reportCount > maxReports)
        {
          throw new BadRequestException(
            UploadErrors.TooManyFiles,
            string.Create(CultureInfo.InvariantCulture, $"An upload holds at most {maxReports} reports.")
          );
        }

        // ARCHITECTURE §5.1: network is one optional part, and the 100 000 lines are counted per part.
        if (part.Kind is UploadPartKind.Network && ++networkCount > 1)
        {
          throw new BadRequestException(UploadErrors.InvalidPart, "An upload holds at most one network part.");
        }

        if (part.FileName.Length > UploadPart.FileNameMaxLength)
        {
          throw new BadRequestException(
            UploadErrors.InvalidPart,
            string.Create(CultureInfo.InvariantCulture, $"The name of a part cannot be longer than {UploadPart.FileNameMaxLength} characters.")
          );
        }

        var stored = await rawStore.WriteAsync(publicId, part, cancellationToken).ConfigureAwait(false);

        hashes.Add(Convert.ToHexStringLower(stored.Sha256));
      }

      if (reportCount == 0)
      {
        throw new BadRequestException(UploadErrors.NoReport, "An upload holds at least one report.");
      }

      return hashes;
    }
    catch (UploadTooLargeException exception)
    {
      await rawStore.DeleteAsync(publicId, CancellationToken.None).ConfigureAwait(false);

      throw new PayloadTooLargeException(UploadErrors.TooLarge, "The content of the upload exceeds a limit.", exception);
    }
    catch (InvalidDataException exception)
    {
      await rawStore.DeleteAsync(publicId, CancellationToken.None).ConfigureAwait(false);

      throw new BadRequestException(UploadErrors.InvalidPart, "A part of the upload is not valid gzip.", exception);
    }
    catch (Exception)
    {
      await rawStore.DeleteAsync(publicId, CancellationToken.None).ConfigureAwait(false);

      throw;
    }
  }

  /// <summary>Finds the tracked repository of an upload, creating it for an account that installed the App.</summary>
  /// <param name="authorization">What the credential proved.</param>
  /// <param name="metadata">What the text fields describe.</param>
  /// <param name="cancellationToken">A token to cancel the work.</param>
  /// <returns>The repository.</returns>
  /// <exception cref="NotFoundException">No installation of the account of the organization serves the repository.</exception>
  private async Task<TrackedRepository> ResolveRepositoryAsync(
    UploadAuthorization authorization,
    UploadMetadata metadata,
    CancellationToken cancellationToken)
  {
    var known = authorization.RepositoryId is { } id
                  ? await repositories.FindAsync(id, cancellationToken).ConfigureAwait(false)
                  : await repositories.FindBySlugAsync(metadata.Slug, cancellationToken).ConfigureAwait(false);

    if (known is not null)
    {
      return known;
    }

    var gateway = gateways.GetGateway(metadata.Forge);
    var connection = authorization.Connection
                     ?? await gateway.GetRepositoryConnectionAsync(metadata.Slug.Owner, metadata.Slug.Name, cancellationToken).ConfigureAwait(false)
                     ?? throw RepositoryNotFound();

    var organization = await directory.FindCurrentAsync(cancellationToken).ConfigureAwait(false) ?? throw RepositoryNotFound();

    if (!string.Equals(connection.ExternalAccountId, organization.ExternalAccountId, StringComparison.OrdinalIgnoreCase))
    {
      throw RepositoryNotFound();
    }

    // Tracked under another name: renamed on its forge since the last upload.
    var renamed = await repositories.FindByExternalIdAsync(connection.RepositoryExternalId, cancellationToken).ConfigureAwait(false);

    return renamed ?? await CreateRepositoryAsync(gateway, metadata.Forge, connection, organization, cancellationToken).ConfigureAwait(false);
  }

  /// <summary>Starts tracking a repository.</summary>
  /// <param name="gateway">The gateway of the forge.</param>
  /// <param name="forge">The forge.</param>
  /// <param name="connection">What the forge reported about the connection.</param>
  /// <param name="organization">The organization of the scope.</param>
  /// <param name="cancellationToken">A token to cancel the work.</param>
  /// <returns>The repository, whichever concurrent upload created it.</returns>
  private async Task<TrackedRepository> CreateRepositoryAsync(
    IForgeGateway gateway,
    Forge forge,
    ForgeConnectionInfo connection,
    Organization organization,
    CancellationToken cancellationToken)
  {
    var address = new ForgeRepositoryRef(forge, connection.ConnectionId, connection.RepositoryExternalId);
    var info = await gateway.GetRepositoryAsync(address, cancellationToken).ConfigureAwait(false) ?? throw RepositoryNotFound();
    var installationId = long.Parse(connection.ConnectionId, NumberStyles.None, CultureInfo.InvariantCulture);

    await EnsureInstallationAsync(installationId, organization.Id, cancellationToken).ConfigureAwait(false);

    var repository = TrackedRepository.Create(
      installationId,
      info.ExternalId,
      RepositorySlug.Create(info.Owner, info.Namespace, info.Name),
      info.DefaultBranch,
      info.IsPrivate,
      secretProtector.Protect(tokenGenerator.NewBadgeToken()),
      clock.UtcNow
    );

    try
    {
      await repositories.AddAsync(repository, cancellationToken).ConfigureAwait(false);

      return repository;
    }
    catch (ConflictException exception) when (exception.ErrorCode == GeneralErrors.ConcurrentChange)
    {
      return await repositories.FindByExternalIdAsync(info.ExternalId, cancellationToken).ConfigureAwait(false) ?? throw RepositoryNotFound();
    }
  }

  /// <summary>Records the installation that serves a repository, under the organization of its account.</summary>
  /// <param name="installationId">The identifier of the installation.</param>
  /// <param name="organizationId">The organization of the scope.</param>
  /// <param name="cancellationToken">A token to cancel the work.</param>
  /// <returns>A task that completes when the installation is known.</returns>
  /// <exception cref="NotFoundException">The installation is held by another organization.</exception>
  private async Task EnsureInstallationAsync(long installationId, long organizationId, CancellationToken cancellationToken)
  {
    var installation = await installations.FindAsync(installationId, cancellationToken).ConfigureAwait(false);

    if (installation is null)
    {
      try
      {
        await installations.AddAsync(Installation.Create(installationId, organizationId, clock.UtcNow), cancellationToken).ConfigureAwait(false);

        return;
      }
      catch (ConflictException exception) when (exception.ErrorCode == GeneralErrors.ConcurrentChange)
      {
        installation = await installations.FindAsync(installationId, cancellationToken).ConfigureAwait(false);
      }
    }

    if (installation?.OrganizationId != organizationId)
    {
      throw RepositoryNotFound();
    }
  }

  #endregion

  #region Implementation of ICommandHandler<ReceiveUploadCommand,ReceivedUpload>

  /// <inheritdoc />
  public async ValueTask<ReceivedUpload> Handle(ReceiveUploadCommand command, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(command);

    var metadata = command.Metadata;
    var authorization = await authenticator.AuthenticateAsync(command.Credential, metadata.Forge, metadata.Slug, cancellationToken).ConfigureAwait(false);
    var repository = await ResolveRepositoryAsync(authorization, metadata, cancellationToken).ConfigureAwait(false);

    if (!repository.IsActive)
    {
      throw new UnprocessableEntityException(RepositoryErrors.Inactive, "The coverage of this repository is not tracked.");
    }

    var now = clock.UtcNow;
    var commit = await commits.GetOrCreateAsync(repository.Id, metadata.Sha, now, cancellationToken).ConfigureAwait(false);

    commit.MarkPending(metadata.Branch, metadata.PullNumber, now);
    await commits.SavePendingAsync(commit, cancellationToken).ConfigureAwait(false);

    var sequence = await commits.NextUploadSequenceAsync(commit.Id, cancellationToken).ConfigureAwait(false);
    var publicId = Guid.NewGuid();
    var hashes = await StoreAsync(publicId, command.Parts, cancellationToken).ConfigureAwait(false);
    var key = command.IdempotencyKey ?? ComputeIdempotencyKey(metadata, hashes);

    var upload = Upload.Receive(
      commit.Id,
      sequence,
      key,
      metadata.Flags,
      metadata.Name,
      metadata.Ci.Service,
      metadata.Ci.BuildId,
      metadata.Ci.JobId,
      metadata.Ci.BuildUrl,
      metadata.Root,
      metadata.UploaderVersion,
      authorization.TokenId,
      authorization.PrincipalKind,
      now,
      publicId
    );

    var job = ProcessUploadJobs.Create(publicId);

    if (await reports.TryAddReceivedUploadAsync(upload, job, cancellationToken).ConfigureAwait(false))
    {
      return new ReceivedUpload(upload, false);
    }

    // A replay: the first reception keeps its row and its parts, those just written are orphans.
    await rawStore.DeleteAsync(publicId, CancellationToken.None).ConfigureAwait(false);

    var existing = await uploads.FindByIdempotencyKeyAsync(commit.Id, key, cancellationToken).ConfigureAwait(false)
                   ?? throw new ConflictException(GeneralErrors.ConcurrentChange, "The upload was removed in the meantime; send it again.");

    return new ReceivedUpload(existing, true);
  }

  #endregion
}
