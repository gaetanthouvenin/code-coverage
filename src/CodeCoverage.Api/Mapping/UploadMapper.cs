// ------------------------------------------------------------------------------------------------
// <copyright file="UploadMapper.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

using Mediator;

using CodeCoverage.Api.Contracts.Uploads;
using CodeCoverage.Api.Uploads;
using CodeCoverage.Application.Configuration;
using CodeCoverage.Application.Exceptions;
using CodeCoverage.Application.Features.Commits;
using CodeCoverage.Application.Features.Uploads;
using CodeCoverage.Application.Features.Uploads.Commands.CompleteUploads;
using CodeCoverage.Application.Features.Uploads.Commands.ReceiveUpload;
using CodeCoverage.Application.Features.Uploads.Queries.AuthorizeUpload;
using CodeCoverage.Application.Features.Uploads.Queries.GetUpload;
using CodeCoverage.Application.Features.Uploads.Queries.GetUploadByCredential;
using CodeCoverage.Domain.Commits;
using CodeCoverage.Domain.Contracts.Uploads;
using CodeCoverage.Domain.Coverage;
using CodeCoverage.Domain.Git;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Repositories;

namespace CodeCoverage.Api.Mapping;

/// <summary>Converts the uploads between the Domain and the API.</summary>
internal static partial class UploadMapper
{
  #region Fields

  private const string SlugField = "slug";

  private const string ForgeField = "forge";

  private const string ShaField = "sha";

  private const string BranchField = "branch";

  private const string PullField = "pr";

  private const string FlagsField = "flags";

  private const string NameField = "name";

  private const string CiServiceField = "ciService";

  private const string BuildUrlField = "buildUrl";

  private const string BuildIdField = "buildId";

  private const string JobIdField = "jobId";

  private const string RootField = "root";

  private const string UploaderVersionField = "uploaderVersion";

  private const string BranchPrefix = "refs/heads/";

  #endregion

  #region Methods

  /// <summary>Checks a credential before anything of the files is read.</summary>
  /// <param name="sender">The request sender.</param>
  /// <param name="credential">The credential.</param>
  /// <param name="fields">The text fields.</param>
  /// <param name="cancellationToken">A token to cancel the request.</param>
  /// <returns>A task that completes when the credential is accepted.</returns>
  /// <exception cref="ArgumentNullException">An argument is <see langword="null" />.</exception>
  public static async Task AuthorizeUploadAsync(
    this ISender sender,
    string credential,
    IReadOnlyDictionary<string, string> fields,
    CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(sender);

    await sender.Send(ToMetadataQuery(credential, fields), cancellationToken).ConfigureAwait(false);
  }

  /// <summary>Receives an upload.</summary>
  /// <param name="sender">The request sender.</param>
  /// <param name="credential">The credential.</param>
  /// <param name="fields">The text fields.</param>
  /// <param name="files">The files, as they arrive.</param>
  /// <param name="idempotencyKey">The value of the <c>Idempotency-Key</c> header, or <see langword="null" />.</param>
  /// <param name="apiBase">The public address of the Api.</param>
  /// <param name="urls">The public addresses of the hosts.</param>
  /// <param name="cancellationToken">A token to cancel the request.</param>
  /// <returns>The answer, and whether the upload had already been received.</returns>
  /// <exception cref="ArgumentNullException">An argument is <see langword="null" />.</exception>
  public static async Task<(UploadAcceptedResponse Response, bool IsReplay)> ReceiveUploadResponseAsync(
    this ISender sender,
    string credential,
    IReadOnlyDictionary<string, string> fields,
    IAsyncEnumerable<MultipartFilePart> files,
    string? idempotencyKey,
    string apiBase,
    UrlOptions urls,
    CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(sender);
    ArgumentNullException.ThrowIfNull(urls);

    var command = ToCommand(credential, fields, files, idempotencyKey);
    var received = await sender.Send(command, cancellationToken).ConfigureAwait(false);
    var metadata = command.Metadata;
    var address = $"{ForgeSegments.ToSegment(metadata.Forge)}/{OwnerSegment(metadata.Slug)}/{metadata.Slug.Name}";
    var commitUrl = $"{(urls.WebPublicUrl ?? apiBase).TrimEnd('/')}/{address}/commit/{metadata.Sha.Value}";
    var statusUrl = $"{(urls.ApiPublicUrl ?? apiBase).TrimEnd('/')}{CodeCoverageApi.Prefix}/repos/{address}/uploads/{received.Upload.PublicId}";

    return (new UploadAcceptedResponse(received.Upload.PublicId, received.Upload.State.ToString(), commitUrl, statusUrl), received.IsReplay);
  }

  /// <summary>Announces the end of the uploads of a commit, with the credential that sends the uploads of its repository.</summary>
  /// <param name="sender">The request sender.</param>
  /// <param name="credential">The credential.</param>
  /// <param name="forge">The forge segment of the address.</param>
  /// <param name="owner">The owner segment of the address.</param>
  /// <param name="repo">The name of the repository.</param>
  /// <param name="sha">The SHA of the commit.</param>
  /// <param name="cancellationToken">A token to cancel the request.</param>
  /// <returns>Where the uploads of the commit stand.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="sender" /> is <see langword="null" />.</exception>
  /// <exception cref="NotFoundException">The forge segment names no forge.</exception>
  public static async Task<UploadsCompleteResponse> CompleteUploadsResponseAsync(
    this ISender sender,
    string credential,
    string forge,
    string owner,
    string repo,
    string sha,
    CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(sender);

    if (!ForgeSegments.TryParse(forge, out var parsed))
    {
      throw new NotFoundException(CommitErrors.NotFound, "The commit does not exist.");
    }

    var completion = await sender.Send(new CompleteUploadsCommand(ToCredential(credential), parsed, owner, repo, sha), cancellationToken).ConfigureAwait(false);

    return new UploadsCompleteResponse(completion.Total, completion.Processed, completion.Processing, completion.Failed);
  }

  /// <summary>Reads an upload with the credential that sends the uploads of its repository.</summary>
  /// <param name="sender">The request sender.</param>
  /// <param name="credential">The credential.</param>
  /// <param name="forge">The forge segment of the address.</param>
  /// <param name="owner">The owner segment of the address.</param>
  /// <param name="repo">The name of the repository.</param>
  /// <param name="id">The public identifier of the upload.</param>
  /// <param name="cancellationToken">A token to cancel the request.</param>
  /// <returns>The upload.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="sender" /> is <see langword="null" />.</exception>
  /// <exception cref="NotFoundException">The forge segment names no forge.</exception>
  public static async Task<UploadResponse> GetUploadResponseAsync(
    this ISender sender,
    string credential,
    string forge,
    string owner,
    string repo,
    Guid id,
    CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(sender);

    if (!ForgeSegments.TryParse(forge, out var parsed))
    {
      throw new NotFoundException(UploadErrors.NotFound, "The upload does not exist or cannot be read.");
    }

    var upload = await sender.Send(new GetUploadByCredentialQuery(ToCredential(credential), parsed, owner, repo, id), cancellationToken).ConfigureAwait(false);

    return ToResponse(upload);
  }

  /// <summary>Reads an upload with a token of the API of a reader of its repository.</summary>
  /// <param name="sender">The request sender.</param>
  /// <param name="forge">The forge segment of the address.</param>
  /// <param name="owner">The owner segment of the address.</param>
  /// <param name="repo">The name of the repository.</param>
  /// <param name="id">The public identifier of the upload.</param>
  /// <param name="cancellationToken">A token to cancel the request.</param>
  /// <returns>The upload.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="sender" /> is <see langword="null" />.</exception>
  public static async Task<UploadResponse> GetUploadForReaderResponseAsync(
    this ISender sender,
    string forge,
    string owner,
    string repo,
    Guid id,
    CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(sender);

    var upload = await sender.Send(new GetUploadQuery(AddressSegments.ToRepositoryForge(forge), owner, repo, id), cancellationToken).ConfigureAwait(false);

    return ToResponse(upload);
  }

  /// <summary>Builds the check of a credential.</summary>
  /// <param name="credential">The credential.</param>
  /// <param name="fields">The text fields.</param>
  /// <returns>The query.</returns>
  /// <exception cref="BadRequestException">A field is not valid.</exception>
  internal static AuthorizeUploadQuery ToMetadataQuery(string credential, IReadOnlyDictionary<string, string> fields)
  {
    return new AuthorizeUploadQuery(ToCredential(credential), ReadForge(fields), fields.GetValueOrDefault(SlugField, string.Empty));
  }

  /// <summary>Builds the reception of an upload.</summary>
  /// <param name="credential">The credential.</param>
  /// <param name="fields">The text fields.</param>
  /// <param name="files">The files, as they arrive.</param>
  /// <param name="idempotencyKey">The value of the <c>Idempotency-Key</c> header, or <see langword="null" />.</param>
  /// <returns>The command.</returns>
  /// <exception cref="BadRequestException">A field is not valid.</exception>
  internal static ReceiveUploadCommand ToCommand(
    string credential,
    IReadOnlyDictionary<string, string> fields,
    IAsyncEnumerable<MultipartFilePart> files,
    string? idempotencyKey)
  {
    if (!RepositorySlug.TryParse(fields.GetValueOrDefault(SlugField), out var slug))
    {
      throw new BadRequestException(UploadErrors.InvalidSlug, "The repository is written owner/name.");
    }

    if (!GitSha.TryParse(fields.GetValueOrDefault(ShaField), out var sha))
    {
      throw new BadRequestException(UploadErrors.InvalidMetadata, "The sha field holds 40 hexadecimal characters.");
    }

    var metadata = new UploadMetadata(
      slug,
      sha,
      ReadBranch(fields),
      ReadPull(fields),
      ReadFlags(fields),
      Optional(fields, NameField),
      Optional(fields, RootField),
      Optional(fields, UploaderVersionField),
      new CiContext(Optional(fields, CiServiceField), Optional(fields, BuildUrlField), Optional(fields, BuildIdField), Optional(fields, JobIdField)),
      ReadForge(fields)
    );

    return new ReceiveUploadCommand(ToCredential(credential), metadata, ToParts(files, CancellationToken.None), string.IsNullOrWhiteSpace(idempotencyKey) ? null : idempotencyKey);
  }

  [GeneratedRegex("^[A-Za-z0-9_-]+\\.[A-Za-z0-9_-]+\\.[A-Za-z0-9_-]+$", RegexOptions.CultureInvariant)]
  private static partial Regex JwtPattern();

  /// <summary>Converts an upload for the API: its state and what its processing reported.</summary>
  /// <param name="upload">The upload.</param>
  /// <returns>The response.</returns>
  internal static UploadResponse ToResponse(Upload upload)
  {
    var log = upload.ProcessingLog;
    var totals = upload.State is UploadState.Processed
                   ? new UploadTotalsModel(upload.Lines, upload.Hits, upload.Misses, upload.Partials, CoverageMath.Ratio(upload.Hits, upload.Lines))
                   : null;

    return new UploadResponse(
      upload.PublicId,
      upload.State.ToString(),
      upload.Flags,
      upload.Name,
      upload.ResolvedFileCount,
      upload.UnresolvedFileCount,
      totals,
      new UploadLogModel(log.Messages, log.MessageTotal, log.Paths, log.PathTotal),
      upload.ErrorCode
    );
  }

  private static UploadCredential ToCredential(string credential) =>
    new(JwtPattern().IsMatch(credential) ? UploadCredentialKind.Jwt : UploadCredentialKind.UploadToken, credential);

  private static async IAsyncEnumerable<UploadPart> ToParts(IAsyncEnumerable<MultipartFilePart> files, [EnumeratorCancellation] CancellationToken cancellationToken)
  {
    await foreach (var file in files.WithCancellation(cancellationToken).ConfigureAwait(false))
    {
      yield return new UploadPart(file.IsNetwork ? UploadPartKind.Network : UploadPartKind.Report, file.FileName, file.Content);
    }
  }

  private static Forge ReadForge(IReadOnlyDictionary<string, string> fields)
  {
    if (!fields.TryGetValue(ForgeField, out var text) || string.IsNullOrWhiteSpace(text))
    {
      return Forge.GitHub;
    }

    return ForgeSegments.TryParse(text, out var forge)
      ? forge
      : throw new BadRequestException(UploadErrors.InvalidMetadata, $"The forge '{text}' is not known.");
  }

  private static string? Optional(IReadOnlyDictionary<string, string> fields, string name) =>
    fields.TryGetValue(name, out var value) && !string.IsNullOrWhiteSpace(value) ? value.Trim() : null;

  private static string? ReadBranch(IReadOnlyDictionary<string, string> fields)
  {
    var branch = Optional(fields, BranchField);

    return branch?.StartsWith(BranchPrefix, StringComparison.Ordinal) is true ? branch[BranchPrefix.Length..] : branch;
  }

  private static int? ReadPull(IReadOnlyDictionary<string, string> fields)
  {
    if (Optional(fields, PullField) is not { } text)
    {
      return null;
    }

    return int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var number)
      ? number
      : throw new BadRequestException(UploadErrors.InvalidMetadata, "The pr field is a number.");
  }

  private static string[] ReadFlags(IReadOnlyDictionary<string, string> fields) =>
    Optional(fields, FlagsField)?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) ?? [];

  private static string OwnerSegment(RepositorySlug slug) =>
    slug.Namespace.Length == 0 ? slug.Owner : $"{slug.Owner}:{slug.Namespace.Replace('/', ':')}";

  #endregion
}
