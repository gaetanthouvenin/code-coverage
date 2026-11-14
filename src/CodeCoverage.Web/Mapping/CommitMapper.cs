// ------------------------------------------------------------------------------------------------
// <copyright file="CommitMapper.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using System.Globalization;
using System.Text.RegularExpressions;

using CodeCoverage.Application.Features.Commits.Commands.ReprocessCommit;
using CodeCoverage.Application.Features.Commits.Queries.GetCommit;
using CodeCoverage.Application.Features.Commits.Queries.GetCommitFiles;
using CodeCoverage.Application.Features.Commits.Queries.ListCommits;
using CodeCoverage.Application.Features.Repositories.Queries.GetEffectiveConfig;
using CodeCoverage.Application.Features.Notifications.Commands.TriggerNotify;
using CodeCoverage.Application.Features.Repositories.Queries.GetRepository;
using CodeCoverage.Application.Features.Uploads.Commands.RetryUpload;
using CodeCoverage.Domain.Commits;
using CodeCoverage.Domain.Config;
using CodeCoverage.Web.ViewModels.Repositories;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Git;
using CodeCoverage.Web.ViewModels.Commits;

using StatusState = CodeCoverage.Domain.Notifications.StatusState;

namespace CodeCoverage.Web.Mapping;

/// <summary>Reads the commits for the pages and converts them to view models: no page names a Domain type.</summary>
internal static partial class CommitMapper
{
  #region Fields

  /// <summary>The number of commits of a page.</summary>
  public const int PageSize = ListCommitsQuery.DefaultPageSize;

  #endregion

  #region Methods

  /// <summary>Reads one page of the commits of a branch, formatted as the configuration of the repository says.</summary>
  /// <param name="sender">The request sender.</param>
  /// <param name="forge">The forge segment of the address.</param>
  /// <param name="owner">The owner segment of the address.</param>
  /// <param name="name">The name of the repository.</param>
  /// <param name="branch">The branch, or <see langword="null" /> for the default one.</param>
  /// <param name="page">The page asked; kept within the pages a list can hold.</param>
  /// <param name="cancellationToken">A token to cancel the reading.</param>
  /// <returns>The page, or <see langword="null" /> when the forge segment is not one the console serves.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="sender" /> is <see langword="null" />.</exception>
  public static async Task<CommitPageViewModel?> GetCommitPageViewModelAsync(
    this ISender sender,
    string forge,
    string owner,
    string name,
    string? branch,
    int page,
    CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(sender);

    if (!RepositoryMapper.TryParseForge(forge, out var parsed))
    {
      return null;
    }

    var overview = await sender.Send(new GetRepositoryQuery(parsed, owner, name), cancellationToken).ConfigureAwait(false);
    var listed = string.IsNullOrEmpty(branch) ? overview.Repository.DefaultBranch : branch;
    var config = (await sender.Send(new GetEffectiveConfigQuery(parsed, owner, name), cancellationToken).ConfigureAwait(false)).Config;
    var query = new ListCommitsQuery(parsed, owner, name, listed, Math.Clamp(page, 1, ListCommitsQuery.MaxPage), PageSize);
    var commits = await sender.Send(query, cancellationToken).ConfigureAwait(false);
    IReadOnlyList<string> branches = [.. overview.Branches.Select(static b => b.Name).Append(overview.Repository.DefaultBranch).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)];

    return new CommitPageViewModel(
      listed,
      branches,
      [.. commits.Items.Select(c => ToRow(c, config))],
      commits.Page,
      commits.PageSize,
      commits.TotalCount
    );
  }

  /// <summary>
  ///   Reads the summary of a commit: its totals against the base of its push comparison, its flags, its posted statuses
  ///   and the configuration stored for it, formatted as the configuration of the repository says.
  /// </summary>
  /// <param name="sender">The request sender.</param>
  /// <param name="forge">The forge segment of the address.</param>
  /// <param name="owner">The owner segment of the address.</param>
  /// <param name="name">The name of the repository.</param>
  /// <param name="sha">The full SHA of the commit.</param>
  /// <param name="cancellationToken">A token to cancel the reading.</param>
  /// <returns>The summary, or <see langword="null" /> when the forge segment or the SHA is not one the console serves.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="sender" /> is <see langword="null" />.</exception>
  public static async Task<CommitDetailViewModel?> GetCommitDetailViewModelAsync(
    this ISender sender,
    string forge,
    string owner,
    string name,
    string sha,
    CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(sender);

    if (!RepositoryMapper.TryParseForge(forge, out var parsed) || !GitSha.TryParse(sha, out var commitSha))
    {
      return null;
    }

    var detail = await sender.Send(new GetCommitQuery(parsed, owner, name, commitSha), cancellationToken).ConfigureAwait(false);
    var config = (await sender.Send(new GetEffectiveConfigQuery(parsed, owner, name), cancellationToken).ConfigureAwait(false)).Config;

    return ToDetail(detail, config);
  }

  /// <summary>
  ///   Reads the files of a commit, or of one of its flags, as a tree: a folder sums the lines of its files and writes
  ///   <c>hits / (hits + misses + partials)</c>, never the mean of its children.
  /// </summary>
  /// <param name="sender">The request sender.</param>
  /// <param name="forge">The forge segment of the address.</param>
  /// <param name="owner">The owner segment of the address.</param>
  /// <param name="name">The name of the repository.</param>
  /// <param name="sha">The full SHA of the commit.</param>
  /// <param name="flag">The flag, or <see langword="null" /> for every file.</param>
  /// <param name="format">How the repository writes a coverage, already read by the page.</param>
  /// <param name="cancellationToken">A token to cancel the reading.</param>
  /// <returns>The tree, or <see langword="null" /> when the forge segment or the SHA is not one the console serves.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="sender" /> or <paramref name="format" /> is <see langword="null" />.</exception>
  public static async Task<FileTreeViewModel?> GetCommitFilesViewModelAsync(
    this ISender sender,
    string forge,
    string owner,
    string name,
    string sha,
    string? flag,
    CoverageFormatViewModel format,
    CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(sender);
    ArgumentNullException.ThrowIfNull(format);

    if (!RepositoryMapper.TryParseForge(forge, out var parsed) || !GitSha.TryParse(sha, out var commitSha))
    {
      return null;
    }

    var query = new GetCommitFilesQuery(parsed, owner, name, commitSha, string.IsNullOrEmpty(flag) ? null : flag, null);
    var files = await sender.Send(query, cancellationToken).ConfigureAwait(false);

    return FileTreeBuilder.Build(files, format.Precision, Enum.Parse<RoundingMode>(format.Rounding));
  }

  /// <summary>Queues the statuses and the comment of a commit to be published again.</summary>
  /// <param name="sender">The request sender.</param>
  /// <param name="forge">The forge segment of the address.</param>
  /// <param name="owner">The owner segment of the address.</param>
  /// <param name="name">The name of the repository.</param>
  /// <param name="sha">The full SHA of the commit.</param>
  /// <param name="cancellationToken">A token to cancel the request.</param>
  /// <returns><see langword="true" /> once queued; <see langword="false" /> when the address is not one the console serves.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="sender" /> is <see langword="null" />.</exception>
  public static async Task<bool> TriggerNotifyAsync(this ISender sender, string forge, string owner, string name, string sha, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(sender);

    if (!RepositoryMapper.TryParseForge(forge, out var parsed) || !GitSha.TryParse(sha, out var commitSha))
    {
      return false;
    }

    await sender.Send(new TriggerNotifyCommand(parsed, owner, name, commitSha), cancellationToken).ConfigureAwait(false);

    return true;
  }

  /// <summary>Queues every upload of a commit to be processed again from its raw files.</summary>
  /// <param name="sender">The request sender.</param>
  /// <param name="forge">The forge segment of the address.</param>
  /// <param name="owner">The owner segment of the address.</param>
  /// <param name="name">The name of the repository.</param>
  /// <param name="sha">The full SHA of the commit.</param>
  /// <param name="cancellationToken">A token to cancel the request.</param>
  /// <returns><see langword="true" /> once queued; <see langword="false" /> when the address is not one the console serves.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="sender" /> is <see langword="null" />.</exception>
  public static async Task<bool> ReprocessCommitAsync(this ISender sender, string forge, string owner, string name, string sha, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(sender);

    if (!RepositoryMapper.TryParseForge(forge, out var parsed) || !GitSha.TryParse(sha, out var commitSha))
    {
      return false;
    }

    await sender.Send(new ReprocessCommitCommand(parsed, owner, name, commitSha), cancellationToken).ConfigureAwait(false);

    return true;
  }

  /// <summary>Queues a failed upload to be processed again from its raw files.</summary>
  /// <param name="sender">The request sender.</param>
  /// <param name="forge">The forge segment of the address.</param>
  /// <param name="owner">The owner segment of the address.</param>
  /// <param name="name">The name of the repository.</param>
  /// <param name="uploadId">The public identifier of the upload.</param>
  /// <param name="cancellationToken">A token to cancel the request.</param>
  /// <returns><see langword="true" /> once queued; <see langword="false" /> when the forge segment is not one the console serves.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="sender" /> is <see langword="null" />.</exception>
  public static async Task<bool> RetryUploadAsync(this ISender sender, string forge, string owner, string name, Guid uploadId, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(sender);

    if (!RepositoryMapper.TryParseForge(forge, out var parsed))
    {
      return false;
    }

    await sender.Send(new RetryUploadCommand(parsed, owner, name, uploadId), cancellationToken).ConfigureAwait(false);

    return true;
  }

  /// <summary>Converts the state of a commit: known and pending are processed, compacted keeps its totals.</summary>
  /// <param name="state">The state.</param>
  /// <returns>The state shown.</returns>
  internal static CommitStatus ToStatus(CommitState state) => state switch
  {
    CommitState.Complete => CommitStatus.Complete,
    CommitState.Compacted => CommitStatus.Compacted,
    CommitState.Error => CommitStatus.Error,
    var _ => CommitStatus.Processing,
  };

  // Only an http or https address becomes a link: the target of a status is written by whoever posted it.
  private static string? SafeUrl(string? url) =>
    Uri.TryCreate(url, UriKind.Absolute, out var parsed) && parsed.Scheme is "https" or "http" ? parsed.AbsoluteUri : null;

  private static string Count(int value) => value.ToString("N0", CultureInfo.CurrentCulture);

  private static CommitDetailViewModel ToDetail(CommitDetail detail, CoverageConfig config)
  {
    var commit = detail.Commit;
    var result = detail.Comparison?.Result;
    var delta = result?.Delta;
    var flagDeltas = result?.Flags.ToDictionary(static f => f.Flag, static f => f.Delta, StringComparer.Ordinal) ?? [];

    return new CommitDetailViewModel(
      DisplayFormatter.ShortSha(commit.Sha),
      Subject(detail.Git?.Message) ?? DisplayFormatter.ShortSha(commit.Sha),
      commit.Branch,
      detail.Git?.AuthorLogin,
      detail.Git?.CommittedAt ?? commit.CompletedAt ?? commit.CreatedAt,
      commit.PullNumber,
      ToStatus(commit.State),
      DisplayFormatter.Percent(commit.Coverage, config.Precision, config.Rounding),
      DisplayFormatter.Delta(delta, config.Precision, config.Rounding),
      delta is { } value ? Math.Sign(value) : 0,
      detail.Comparison?.ComparedToSha is { } baseSha && result?.Base is not null ? DisplayFormatter.ShortSha(baseSha) : null,
      result?.Base is { } @base ? DisplayFormatter.Percent(@base.Coverage, config.Precision, config.Rounding) : null,
      Count(commit.Files),
      detail.BaseFiles is { } baseFiles && detail.Comparison?.ComparedToSha is not null ? Count(baseFiles) : null,
      Count(commit.Lines),
      Count(commit.Hits),
      Count(commit.Partials),
      Count(commit.Misses),
      commit.SessionCount,
      detail.Sessions.Count(static s => s.Kind is SessionKind.CarriedForward),
      [
        .. detail.Sessions.Where(static s => s is { Kind: SessionKind.CarriedForward, CarriedForwardFromSha: not null })
                 .Select(static s => new CarriedOriginViewModel(s.Flags.Count == 0 ? DisplayFormatter.Dash : string.Join(", ", s.Flags), DisplayFormatter.ShortSha(s.CarriedForwardFromSha!)))
                 .Distinct(),
      ],
      [
        .. detail.Flags.Where(static f => f.Flag.Length > 0)
                 .OrderBy(static f => f.Flag, StringComparer.Ordinal)
                 .Select(f => ToFlag(f, flagDeltas.GetValueOrDefault(f.Flag), config)),
      ],
      [.. detail.Statuses.Select(static s => new PostedStatusViewModel(s.Status.Context, ToTone(s.Status.State), s.Status.Description, SafeUrl(s.TargetUrl)))],
      ToConfig(detail.Config, commit.ConfigSource),
      ToUploadRows(detail),
      RepositoryMapper.ToFormat(config)
    );
  }

  // Every upload of the commit by sequence, whatever its state, then the sessions carried forward to it by number. An
  // upload is counted in the commit once its session exists: the session names its upload by identifier, never by a
  // number a carried session may share.
  private static IReadOnlyList<UploadSessionRowViewModel> ToUploadRows(CommitDetail detail)
  {
    var counted = detail.Sessions.Where(static s => s is { Kind: SessionKind.Uploaded, UploadId: not null })
                        .Select(static s => s.UploadId!.Value)
                        .ToHashSet();

    return
    [
      .. detail.Uploads.OrderBy(static u => u.Sequence).Select(u => ToUploadRow(u, counted.Contains(u.Id))),
      .. detail.Sessions.Where(static s => s.Kind is SessionKind.CarriedForward).OrderBy(static s => s.Number).Select(ToCarriedRow),
    ];
  }

  private static UploadSessionRowViewModel ToUploadRow(Upload upload, bool counted)
  {
    var status = upload.State switch
    {
      UploadState.Failed => UploadStatus.Failed,
      UploadState.Processed when counted => UploadStatus.Processed,
      var _ => UploadStatus.Processing,
    };
    var log = upload.ProcessingLog;

    return new UploadSessionRowViewModel(
      $"u{upload.Sequence}",
      upload.Sequence,
      upload.Name,
      status,
      upload.PublicId,
      CiLabel(upload.CiService, upload.BuildId),
      SafeUrl(upload.BuildUrl),
      upload.Flags,
      upload.State is UploadState.Processed or UploadState.Failed ? Count(upload.ResolvedFileCount) : null,
      upload.ResolvedFileCount,
      null,
      status is UploadStatus.Failed ? upload.ErrorCode : null,
      new ProcessingLogViewModel(
        [.. log.Messages.Select(ToLogMessage)],
        log.MessageTotal - log.Messages.Count,
        log.Paths,
        log.PathTotal - log.Paths.Count)
    );
  }

  private static UploadSessionRowViewModel ToCarriedRow(Session session) =>
    new(
      $"s{session.Number}",
      session.Number,
      session.Name,
      UploadStatus.CarriedForward,
      null,
      CiLabel(session.CiService, null),
      SafeUrl(session.BuildUrl),
      session.Flags,
      null,
      0,
      session.CarriedForwardFromSha is { } from ? DisplayFormatter.ShortSha(from) : null,
      null,
      ProcessingLogViewModel.Empty
    );

  // The uploader names the two services it detects by their identifiers; any other is shown as written.
  private static string? CiLabel(string? service, string? build)
  {
    var name = service switch
    {
      null or "" => null,
      "github-actions" => "GitHub Actions",
      "azure-pipelines" => "Azure Pipelines",
      var other => other,
    };

    return name is null ? null : string.IsNullOrEmpty(build) ? name : $"{name} #{build}";
  }

  // « Upload.UnsupportedFormat: coverage.json » carries a code the console translates; any other message is free text.
  private static LogMessageViewModel ToLogMessage(string message) =>
    LogCode().Match(message) is { Success: true } match
      ? new LogMessageViewModel(match.Groups["code"].Value, match.Groups["argument"].Value)
      : new LogMessageViewModel(null, message);

  [GeneratedRegex(@"^(?<code>Upload\.[A-Za-z]+): (?<argument>.*)$", RegexOptions.Singleline | RegexOptions.CultureInvariant, 1000)]
  private static partial Regex LogCode();

  private static CommitFlagRowViewModel ToFlag(CommitFlagTotals totals, decimal? delta, CoverageConfig config) =>
    new(
      totals.Flag,
      DisplayFormatter.Percent(totals.Coverage, config.Precision, config.Rounding),
      DisplayFormatter.Delta(delta, config.Precision, config.Rounding),
      delta is { } value ? Math.Sign(value) : 0,
      totals is { IsCarriedForward: true, CarriedForwardFromSha: { } from } ? DisplayFormatter.ShortSha(from) : null
    );

  private static StatusTone ToTone(StatusState state) => state switch
  {
    StatusState.Success => StatusTone.Success,
    StatusState.Failure => StatusTone.Failure,
    var _ => StatusTone.Error,
  };

  // The stored row says what was really applied; without it, the source recorded on the commit is shown as it is.
  // An imported commit has no stored row: its source is named, not shown as the raw text.
  private static CommitConfigViewModel ToConfig(CommitConfig? stored, string? recorded) =>
    stored is null
      ? new CommitConfigViewModel(recorded is Commit.ImportedConfigSource ? "ConfigSource_CodecovImport" : null, recorded, null, [], [])
      : new CommitConfigViewModel(
        $"ConfigSource_{stored.Source}",
        recorded,
        stored.Path,
        [.. stored.Warnings.Select(static w => new ConfigWarningViewModel(w.Code, w.Key, w.Detail))],
        stored.Errors
      );

  // The first line of the message: the subject of the commit.
  private static string? Subject(string? message) => message?.Split('\n', 2)[0].TrimEnd('\r');

  /// <summary>Reads the newest commits of a branch, for the panel of the overview.</summary>
  /// <param name="sender">The request sender.</param>
  /// <param name="forge">The forge of the repository.</param>
  /// <param name="owner">The owner segment of the address.</param>
  /// <param name="name">The name of the repository.</param>
  /// <param name="branch">The branch to read.</param>
  /// <param name="format">How the repository writes a coverage, already read by the overview.</param>
  /// <param name="count">The number of rows.</param>
  /// <param name="cancellationToken">A token to cancel the reading.</param>
  /// <returns>The rows, the newest first.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="sender" /> or <paramref name="format" /> is <see langword="null" />.</exception>
  /// <remarks>The repository and its configuration are not read again: the overview passes what it already has.</remarks>
  internal static async Task<IReadOnlyList<CommitRowViewModel>> ListRecentCommitRowsAsync(
    this ISender sender,
    Forge forge,
    string owner,
    string name,
    string branch,
    CoverageFormatViewModel format,
    int count,
    CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(sender);
    ArgumentNullException.ThrowIfNull(format);

    var commits = await sender.Send(new ListCommitsQuery(forge, owner, name, branch, 1, count), cancellationToken).ConfigureAwait(false);
    var rounding = Enum.Parse<RoundingMode>(format.Rounding);

    return [.. commits.Items.Select(c => ToRow(c, format.Precision, rounding))];
  }

  private static CommitRowViewModel ToRow(CommitSummary summary, CoverageConfig config) => ToRow(summary, config.Precision, config.Rounding);

  private static CommitRowViewModel ToRow(CommitSummary summary, int precision, RoundingMode rounding)
  {
    var commit = summary.Commit;

    return new CommitRowViewModel(
      commit.Sha.Value,
      DisplayFormatter.ShortSha(commit.Sha),
      Subject(summary.Git?.Message),
      summary.Git?.AuthorLogin,
      summary.Git?.CommittedAt ?? commit.CompletedAt ?? commit.CreatedAt,
      DisplayFormatter.Percent(commit.Coverage, precision, rounding),
      DisplayFormatter.Delta(summary.Delta, precision, rounding),
      summary.Delta is { } delta ? Math.Sign(delta) : 0,
      ToStatus(commit.State),
      commit.SessionCount
    );
  }

  #endregion
}
