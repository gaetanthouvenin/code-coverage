// ------------------------------------------------------------------------------------------------
// <copyright file="Upload.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Guards;

namespace CodeCoverage.Domain.Commits;

/// <summary>A set of coverage reports received for a commit.</summary>
public sealed class Upload
{
  #region Fields

  /// <summary>The maximum length of <see cref="IdempotencyKey" />.</summary>
  public const int IdempotencyKeyMaxLength = 128;

  /// <summary>The maximum length of <see cref="Name" />.</summary>
  public const int NameMaxLength = 255;

  /// <summary>The maximum length of <see cref="CiService" />.</summary>
  public const int CiServiceMaxLength = 64;

  /// <summary>The maximum length of <see cref="BuildId" /> and <see cref="JobId" />.</summary>
  public const int BuildIdMaxLength = 255;

  /// <summary>The maximum length of <see cref="BuildUrl" />.</summary>
  public const int BuildUrlMaxLength = 2048;

  /// <summary>The maximum length of <see cref="RepoRoot" />.</summary>
  public const int RepoRootMaxLength = 1024;

  /// <summary>The maximum length of <see cref="UploaderVersion" />.</summary>
  public const int UploaderVersionMaxLength = 64;

  /// <summary>The maximum length of <see cref="ErrorCode" />.</summary>
  public const int ErrorCodeMaxLength = 128;

  /// <summary>The maximum length of <see cref="LockedBy" />.</summary>
  public const int LockedByMaxLength = 128;

  #endregion

  #region Ctors

  /// <summary>Initializes a new instance of the <see cref="Upload" /> class.</summary>
  /// <param name="publicId">The public identifier.</param>
  /// <param name="commitId">The commit.</param>
  /// <param name="sequence">The session number.</param>
  /// <param name="idempotencyKey">The key that recognizes a replay.</param>
  /// <param name="principalKind">The kind of credential that sent the upload.</param>
  /// <param name="receivedAt">When it was received, in UTC.</param>
  private Upload(
    Guid publicId,
    long commitId,
    int sequence,
    string idempotencyKey,
    PrincipalKind principalKind,
    DateTimeOffset receivedAt)
  {
    PublicId = publicId;
    CommitId = commitId;
    Sequence = sequence;
    IdempotencyKey = idempotencyKey;
    PrincipalKind = principalKind;
    ReceivedAt = receivedAt;
  }

  #endregion

  #region Properties

  /// <summary>Gets the identifier of the row, assigned when it is saved.</summary>
  public long Id { get; private set; }

  /// <summary>Gets the identifier shown outside, which cannot be guessed.</summary>
  public Guid PublicId { get; private set; }

  /// <summary>Gets the identifier of the commit.</summary>
  public long CommitId { get; private set; }

  /// <summary>Gets the session number, stable from one revision of the report to the next.</summary>
  public int Sequence { get; private set; }

  /// <summary>Gets the key that recognizes a replay of the same upload on its commit.</summary>
  public string IdempotencyKey { get; private set; }

  /// <summary>Gets where the upload stands in its processing.</summary>
  public UploadState State { get; private set; } = UploadState.Received;

  /// <summary>Gets the flags of the upload.</summary>
  public IReadOnlyList<string> Flags { get; private set; } = [];

  /// <summary>Gets the name of the session, or <see langword="null" />.</summary>
  public string? Name { get; private set; }

  /// <summary>Gets the CI service that sent it, or <see langword="null" />.</summary>
  public string? CiService { get; private set; }

  /// <summary>Gets the identifier of the build, or <see langword="null" />.</summary>
  public string? BuildId { get; private set; }

  /// <summary>Gets the identifier of the job, or <see langword="null" />.</summary>
  public string? JobId { get; private set; }

  /// <summary>Gets the address of the build, or <see langword="null" />.</summary>
  public string? BuildUrl { get; private set; }

  /// <summary>Gets the absolute root of the repository on the agent, or <see langword="null" />.</summary>
  public string? RepoRoot { get; private set; }

  /// <summary>Gets the version of the uploader, or <see langword="null" />.</summary>
  public string? UploaderVersion { get; private set; }

  /// <summary>Gets the identifier of the upload token, or <see langword="null" /> for an OIDC token.</summary>
  public long? TokenId { get; private set; }

  /// <summary>Gets the kind of credential that sent the upload.</summary>
  public PrincipalKind PrincipalKind { get; private set; }

  /// <summary>Gets the number of reports it held.</summary>
  public int ReportFileCount { get; private set; }

  /// <summary>Gets the number of files whose path was resolved.</summary>
  public int ResolvedFileCount { get; private set; }

  /// <summary>Gets the number of files whose path could not be resolved.</summary>
  public int UnresolvedFileCount { get; private set; }

  /// <summary>Gets the number of coverable lines.</summary>
  public int Lines { get; private set; }

  /// <summary>Gets the number of covered lines.</summary>
  public int Hits { get; private set; }

  /// <summary>Gets the number of lines that are not covered.</summary>
  public int Misses { get; private set; }

  /// <summary>Gets the number of partially covered lines.</summary>
  public int Partials { get; private set; }

  /// <summary>Gets the code of its failure, or <see langword="null" />.</summary>
  public string? ErrorCode { get; private set; }

  /// <summary>Gets the description of its failure, or <see langword="null" />.</summary>
  public string? Error { get; private set; }

  /// <summary>Gets what its processing reported.</summary>
  public ProcessingLog ProcessingLog { get; private set; } = ProcessingLog.Empty;

  /// <summary>Gets when it was received, in UTC.</summary>
  public DateTimeOffset ReceivedAt { get; private set; }

  /// <summary>Gets when its processing ended, or <see langword="null" />.</summary>
  public DateTimeOffset? ProcessedAt { get; private set; }

  /// <summary>Gets the worker that processes it, or <see langword="null" />.</summary>
  public string? LockedBy { get; private set; }

  #endregion

  #region Methods

  /// <summary>Records an upload that was just received.</summary>
  /// <param name="commitId">The identifier of the commit.</param>
  /// <param name="sequence">The session number given by the commit.</param>
  /// <param name="idempotencyKey">The key that recognizes a replay.</param>
  /// <param name="flags">The flags of the upload.</param>
  /// <param name="name">The name of the session, or <see langword="null" />.</param>
  /// <param name="ciService">The CI service, or <see langword="null" />.</param>
  /// <param name="buildId">The identifier of the build, or <see langword="null" />.</param>
  /// <param name="jobId">The identifier of the job, or <see langword="null" />.</param>
  /// <param name="buildUrl">The address of the build, or <see langword="null" />.</param>
  /// <param name="repoRoot">The root of the repository on the agent, or <see langword="null" />.</param>
  /// <param name="uploaderVersion">The version of the uploader, or <see langword="null" />.</param>
  /// <param name="tokenId">The identifier of the upload token, or <see langword="null" />.</param>
  /// <param name="principalKind">The kind of credential that sent the upload.</param>
  /// <param name="at">The current time; converted to UTC.</param>
  /// <returns>The upload, not saved yet.</returns>
  /// <exception cref="ArgumentOutOfRangeException">
  ///   <paramref name="commitId" /> or <paramref name="sequence" /> is not positive, or
  ///   <paramref name="principalKind" /> is not a defined value.
  /// </exception>
  /// <exception cref="ArgumentNullException">
  ///   <paramref name="idempotencyKey" /> or <paramref name="flags" /> is <see langword="null" />.
  /// </exception>
  /// <exception cref="ArgumentException"><paramref name="idempotencyKey" /> is empty or white space, or a text is too long.</exception>
  public static Upload Receive(
    long commitId,
    int sequence,
    string idempotencyKey,
    IReadOnlyList<string> flags,
    string? name,
    string? ciService,
    string? buildId,
    string? jobId,
    string? buildUrl,
    string? repoRoot,
    string? uploaderVersion,
    long? tokenId,
    PrincipalKind principalKind,
    DateTimeOffset at)
  {
    ArgumentOutOfRangeException.ThrowIfNegativeOrZero(commitId);
    ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sequence);
    ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey);
    ArgumentNullException.ThrowIfNull(flags);
    ArgumentOutOfRangeException.ThrowIfUndefined(principalKind);

    ThrowIfTooLong(idempotencyKey, IdempotencyKeyMaxLength, nameof(idempotencyKey));
    ThrowIfTooLong(name, NameMaxLength, nameof(name));
    ThrowIfTooLong(ciService, CiServiceMaxLength, nameof(ciService));
    ThrowIfTooLong(buildId, BuildIdMaxLength, nameof(buildId));
    ThrowIfTooLong(jobId, BuildIdMaxLength, nameof(jobId));
    ThrowIfTooLong(buildUrl, BuildUrlMaxLength, nameof(buildUrl));
    ThrowIfTooLong(repoRoot, RepoRootMaxLength, nameof(repoRoot));
    ThrowIfTooLong(uploaderVersion, UploaderVersionMaxLength, nameof(uploaderVersion));

    return new Upload(Guid.NewGuid(), commitId, sequence, idempotencyKey, principalKind, at.ToUniversalTime())
    {
      Flags = [.. flags],
      Name = name,
      CiService = ciService,
      BuildId = buildId,
      JobId = jobId,
      BuildUrl = buildUrl,
      RepoRoot = repoRoot,
      UploaderVersion = uploaderVersion,
      TokenId = tokenId,
    };
  }

  /// <summary>Refuses a text longer than its column.</summary>
  /// <param name="value">The text, or <see langword="null" />.</param>
  /// <param name="maxLength">The maximum length.</param>
  /// <param name="paramName">The name of the parameter.</param>
  /// <exception cref="ArgumentException"><paramref name="value" /> is too long.</exception>
  private static void ThrowIfTooLong(string? value, int maxLength, string paramName) =>
    ArgumentException.ThrowIf(value?.Length > maxLength, $"The value cannot be longer than {maxLength} characters.", paramName);

  #endregion
}
