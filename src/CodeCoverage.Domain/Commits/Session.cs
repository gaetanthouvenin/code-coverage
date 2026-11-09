// ------------------------------------------------------------------------------------------------
// <copyright file="Session.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Coverage;
using CodeCoverage.Domain.Git;
using CodeCoverage.Domain.Guards;

namespace CodeCoverage.Domain.Commits;

/// <summary>One source of coverage in the report of a commit: an upload, or a session carried forward.</summary>
/// <remarks>The sessions of a commit are removed and written again by each finalization.</remarks>
public sealed class Session
{
  #region Fields

  /// <summary>The maximum length of <see cref="Name" />: the one of an upload and the prefix of a carried-forward session.</summary>
  public const int NameMaxLength = Upload.NameMaxLength + 16;

  #endregion

  #region Ctors

  /// <summary>Initializes a new instance of the <see cref="Session" /> class.</summary>
  /// <param name="commitId">The commit.</param>
  /// <param name="kind">Where its coverage comes from.</param>
  /// <param name="number">The number of the session in the report.</param>
  private Session(long commitId, SessionKind kind, int number)
  {
    CommitId = commitId;
    Kind = kind;
    Number = number;
  }

  #endregion

  #region Properties

  /// <summary>Gets the identifier of the row, assigned when it is saved.</summary>
  public long Id { get; private set; }

  /// <summary>Gets the identifier of the commit.</summary>
  public long CommitId { get; private set; }

  /// <summary>Gets where its coverage comes from.</summary>
  public SessionKind Kind { get; private set; }

  /// <summary>Gets the number of the session in the report.</summary>
  public int Number { get; private set; }

  /// <summary>Gets the identifier of its upload, or <see langword="null" /> for a session carried forward.</summary>
  public long? UploadId { get; private set; }

  /// <summary>Gets the SHA its coverage was first carried from, or <see langword="null" /> for an uploaded session.</summary>
  public GitSha? CarriedForwardFromSha { get; private set; }

  /// <summary>Gets the identifier of the session it was copied from, or <see langword="null" /> for an uploaded session.</summary>
  /// <remarks>Informative: the sessions of the ancestor are written again by its next finalization.</remarks>
  public long? SourceSessionId { get; private set; }

  /// <summary>Gets the upload whose blob holds its coverage, or <see langword="null" /> when it has its own <see cref="Data" />.</summary>
  public long? DataUploadId { get; private set; }

  /// <summary>Gets the blob of a session whose lines were shifted, or <see langword="null" />.</summary>
  public byte[]? Data { get; private set; }

  /// <summary>Gets its flags.</summary>
  public IReadOnlyList<string> Flags { get; private set; } = [];

  /// <summary>Gets its name, or <see langword="null" /> when the upload had none, as in Codecov.</summary>
  public string? Name { get; private set; }

  /// <summary>Gets the CI service that sent it, or <see langword="null" />.</summary>
  public string? CiService { get; private set; }

  /// <summary>Gets the address of its build, or <see langword="null" />.</summary>
  public string? BuildUrl { get; private set; }

  /// <summary>Gets the number of coverable lines.</summary>
  public int Lines { get; private set; }

  /// <summary>Gets the number of covered lines.</summary>
  public int Hits { get; private set; }

  /// <summary>Gets the number of lines that are not covered.</summary>
  public int Misses { get; private set; }

  /// <summary>Gets the number of partially covered lines.</summary>
  public int Partials { get; private set; }

  /// <summary>Gets the coverage in percent, or <see langword="null" /> without a coverable line.</summary>
  public decimal? Coverage { get; private set; }

  #endregion

  #region Methods

  /// <summary>Creates the session of an upload.</summary>
  /// <param name="commitId">The identifier of the commit.</param>
  /// <param name="number">The number of the session, the sequence of the upload.</param>
  /// <param name="uploadId">The identifier of the upload.</param>
  /// <param name="flags">Its flags.</param>
  /// <param name="name">Its name, or <see langword="null" />.</param>
  /// <param name="ciService">The CI service, or <see langword="null" />.</param>
  /// <param name="buildUrl">The address of the build, or <see langword="null" />.</param>
  /// <param name="totals">The counters of its coverage.</param>
  /// <returns>The session, not saved yet.</returns>
  /// <exception cref="ArgumentOutOfRangeException"><paramref name="commitId" />, <paramref name="number" /> or <paramref name="uploadId" /> is not positive.</exception>
  /// <exception cref="ArgumentNullException"><paramref name="flags" /> is <see langword="null" />.</exception>
  /// <exception cref="ArgumentException">A text is too long.</exception>
  public static Session FromUpload(
    long commitId,
    int number,
    long uploadId,
    IReadOnlyList<string> flags,
    string? name,
    string? ciService,
    string? buildUrl,
    CoverageTotals totals)
  {
    ArgumentOutOfRangeException.ThrowIfNegativeOrZero(uploadId);

    var session = Describe(commitId, SessionKind.Uploaded, number, flags, name, ciService, buildUrl, totals);
    session.UploadId = uploadId;

    return session;
  }

  /// <summary>Creates a session copied from an ancestor.</summary>
  /// <param name="commitId">The identifier of the commit.</param>
  /// <param name="number">The number of the session.</param>
  /// <param name="carriedForwardFromSha">The SHA its coverage was first carried from.</param>
  /// <param name="sourceSessionId">The identifier of the session it was copied from.</param>
  /// <param name="dataUploadId">The upload whose blob holds its coverage, when no line was shifted.</param>
  /// <param name="data">Its own blob, when lines were shifted.</param>
  /// <param name="flags">Its flags.</param>
  /// <param name="name">Its name, or <see langword="null" />.</param>
  /// <param name="ciService">The CI service, or <see langword="null" />.</param>
  /// <param name="buildUrl">The address of the build, or <see langword="null" />.</param>
  /// <param name="totals">The counters of its coverage.</param>
  /// <returns>The session, not saved yet.</returns>
  /// <exception cref="ArgumentOutOfRangeException">An identifier or <paramref name="number" /> is not positive.</exception>
  /// <exception cref="ArgumentNullException"><paramref name="carriedForwardFromSha" /> or <paramref name="flags" /> is <see langword="null" />.</exception>
  /// <exception cref="ArgumentException">
  ///   A text is too long, <paramref name="data" /> is empty, or <paramref name="dataUploadId" /> and <paramref name="data" /> are not exclusive.
  /// </exception>
  public static Session CarriedForward(
    long commitId,
    int number,
    GitSha carriedForwardFromSha,
    long sourceSessionId,
    long? dataUploadId,
    byte[]? data,
    IReadOnlyList<string> flags,
    string? name,
    string? ciService,
    string? buildUrl,
    CoverageTotals totals)
  {
    ArgumentNullException.ThrowIfNull(carriedForwardFromSha);
    ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sourceSessionId);

    if (dataUploadId is { } uploadId)
    {
      ArgumentOutOfRangeException.ThrowIfNegativeOrZero(uploadId, nameof(dataUploadId));
    }

    ArgumentException.ThrowIf(dataUploadId is null == data is null, "A carried-forward session has either an upload or its own blob.", nameof(data));
    ArgumentException.ThrowIf(data?.Length == 0, "The blob cannot be empty.", nameof(data));

    var session = Describe(commitId, SessionKind.CarriedForward, number, flags, name, ciService, buildUrl, totals);
    session.CarriedForwardFromSha = carriedForwardFromSha;
    session.SourceSessionId = sourceSessionId;
    session.DataUploadId = dataUploadId;
    session.Data = data;

    return session;
  }

  /// <summary>Builds what both kinds of sessions hold.</summary>
  /// <param name="commitId">The identifier of the commit.</param>
  /// <param name="kind">Where its coverage comes from.</param>
  /// <param name="number">The number of the session.</param>
  /// <param name="flags">Its flags.</param>
  /// <param name="name">Its name, or <see langword="null" />.</param>
  /// <param name="ciService">The CI service, or <see langword="null" />.</param>
  /// <param name="buildUrl">The address of the build, or <see langword="null" />.</param>
  /// <param name="totals">The counters of its coverage.</param>
  /// <returns>The session.</returns>
  /// <exception cref="ArgumentOutOfRangeException"><paramref name="commitId" /> or <paramref name="number" /> is not positive.</exception>
  /// <exception cref="ArgumentNullException"><paramref name="flags" /> is <see langword="null" />.</exception>
  /// <exception cref="ArgumentException">A text is too long.</exception>
  private static Session Describe(
    long commitId,
    SessionKind kind,
    int number,
    IReadOnlyList<string> flags,
    string? name,
    string? ciService,
    string? buildUrl,
    CoverageTotals totals)
  {
    ArgumentOutOfRangeException.ThrowIfNegativeOrZero(commitId);
    ArgumentOutOfRangeException.ThrowIfNegativeOrZero(number);
    ArgumentNullException.ThrowIfNull(flags);

    ThrowIfTooLong(name, NameMaxLength, nameof(name));
    ThrowIfTooLong(ciService, Upload.CiServiceMaxLength, nameof(ciService));
    ThrowIfTooLong(buildUrl, Upload.BuildUrlMaxLength, nameof(buildUrl));

    return new Session(commitId, kind, number)
    {
      Flags = [.. flags],
      Name = name,
      CiService = ciService,
      BuildUrl = buildUrl,
      Lines = totals.Lines,
      Hits = totals.Hits,
      Misses = totals.Misses,
      Partials = totals.Partials,
      Coverage = totals.Coverage,
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
