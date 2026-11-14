// ------------------------------------------------------------------------------------------------
// <copyright file="GitHubAppGateway.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Buffers;
using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using CodeCoverage.Application.Exceptions;
using CodeCoverage.Application.Security;
using CodeCoverage.Domain.Contracts.Forges;
using CodeCoverage.Domain.Contracts.GitHub;
using CodeCoverage.Domain.Git;
using CodeCoverage.Domain.Guards;
using CodeCoverage.Domain.Notifications;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Infrastructure.GitHub.Models;

namespace CodeCoverage.Infrastructure.GitHub;

/// <summary>The readings of GitHub, through its REST API and the GitHub App.</summary>
/// <param name="client">The <c>github</c> client, whose pipeline adds the API version and handles the rate limits.</param>
/// <param name="tokens">The authentication of the server against GitHub.</param>
/// <param name="options">The settings of the App, read at each use.</param>
/// <param name="logger">The logger.</param>
/// <remarks>
///   A tracked repository is read by its identifier, through <c>/repositories/{id}</c>, so a renaming never breaks a
///   reading; only the search of a repository that is not tracked yet goes by owner and name. A refusal other than the
///   absence of what was asked for is an answer no caller can use: it surfaces as a <see cref="BadGatewayException" />.
/// </remarks>
internal sealed partial class GitHubAppGateway(
  HttpClient client,
  GitHubAppTokenProvider tokens,
  IOptionsMonitor<GitHubOptions> options,
  ILogger<GitHubAppGateway> logger) : IForgeGateway, IGitHubAppGateway
{
  #region Fields

  private const string RawMediaType = "application/vnd.github.raw+json";

  /// <summary>The refusal of a resource that does not exist.</summary>
  /// <summary>The size of a page of the files of a pull request.</summary>
  private const int PullFilesPerPage = 50;

  /// <summary>The number of pages that reach the 3000 files GitHub lists at most for a pull request.</summary>
  private const int PullFilesMaxPages = 60;

  private const int ListPerPage = 100;

  // 100 000 installations or repositories: far beyond what one instance serves, and an end to a listing that never shrinks.
  private const int ListMaxPages = 1000;

  // 10 000 comments on one pull request.
  private const int CommentsMaxPages = 100;

  private static readonly HttpStatusCode[] NotFound = [HttpStatusCode.NotFound];

  /// <summary>
  ///   The refusals of a new comment that are not failures: the pull request is gone, locked, or no longer takes
  ///   comments.
  /// </summary>
  private static readonly HttpStatusCode[] CommentRefused =
  [
    HttpStatusCode.NotFound,
    HttpStatusCode.Forbidden,
    HttpStatusCode.Gone,
    HttpStatusCode.UnprocessableEntity,
  ];

  /// <summary>The refusals of the code of a manifest that is unknown, expired or already used.</summary>
  private static readonly HttpStatusCode[] ManifestCodeRefused = [HttpStatusCode.NotFound, HttpStatusCode.UnprocessableEntity];

  /// <summary>
  ///   The refusals of a git object that cannot be read: unknown (404 or 422), or in a repository that is still empty
  ///   (409).
  /// </summary>
  private static readonly HttpStatusCode[] NoSuchGitObject =
  [
    HttpStatusCode.NotFound,
    HttpStatusCode.Conflict,
    HttpStatusCode.UnprocessableEntity,
  ];

  #endregion

  #region Methods

  /// <inheritdoc />
  /// <exception cref="BadGatewayException">GitHub refused the reading, does not answer or answered with an unreadable payload.</exception>
  /// <exception cref="GitHubRateLimitedException">GitHub rate limited the request.</exception>
  /// <remarks>GitHub reports the installation without the repository, hence a second reading with the installation token.</remarks>
  public async Task<ForgeConnectionInfo?> GetRepositoryConnectionAsync(
    string owner,
    string name,
    CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(owner);
    ArgumentNullException.ThrowIfNull(name);

    if (!IsPathSegment(owner) || !IsPathSegment(name))
    {
      return null;
    }

    var path = $"repos/{Uri.EscapeDataString(owner)}/{Uri.EscapeDataString(name)}";

    var installation = await ReadAsAppAsync($"{path}/installation", cancellationToken).ConfigureAwait(false);

    if (installation is null)
    {
      return null;
    }

    return await GetAsync(
             installation.Id,
             path,
             GitHubJsonContext.Default.GitHubRepositoryResponse,
             NotFound,
             installation.ToForgeConnection,
             cancellationToken
           ).ConfigureAwait(false);
  }

  /// <inheritdoc />
  /// <exception cref="ArgumentException">The reference does not designate a GitHub repository.</exception>
  /// <exception cref="BadGatewayException">GitHub refused the reading, does not answer or answered with an unreadable payload.</exception>
  /// <exception cref="GitHubRateLimitedException">GitHub rate limited the request.</exception>
  public Task<ForgeRepositoryInfo?> GetRepositoryAsync(ForgeRepositoryRef repository, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(repository);

    var (installationId, root) = Locate(repository);

    return GetAsync(
      installationId,
      root,
      GitHubJsonContext.Default.GitHubRepositoryResponse,
      NotFound,
      GitHubGatewayMapper.ToForgeRepository,
      cancellationToken
    );
  }

  /// <inheritdoc />
  /// <exception cref="ArgumentException">The reference does not designate a GitHub repository.</exception>
  /// <exception cref="BadGatewayException">GitHub refused the reading, does not answer or answered with an unreadable payload.</exception>
  /// <exception cref="GitHubRateLimitedException">GitHub rate limited the request.</exception>
  public async Task<IReadOnlyList<ForgeCommitInfo>> ListCommitsAsync(
    ForgeRepositoryRef repository,
    GitSha sha,
    int perPage,
    CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(repository);
    ArgumentNullException.ThrowIfNull(sha);
    ArgumentOutOfRangeException.ThrowIfNegativeOrZero(perPage);

    var (installationId, root) = Locate(repository);

    var commits = await GetAsync<IReadOnlyList<GitHubCommitResponse>, IReadOnlyList<ForgeCommitInfo>>(
                    installationId,
                    string.Create(CultureInfo.InvariantCulture, $"{root}/commits?sha={sha.Value}&per_page={perPage}"),
                    GitHubJsonContext.Default.IReadOnlyListGitHubCommitResponse,
                    NoSuchGitObject,
                    static list => [.. list.Select(GitHubGatewayMapper.ToForgeCommit)],
                    cancellationToken
                  ).ConfigureAwait(false);

    return commits ?? [];
  }

  /// <inheritdoc />
  /// <exception cref="ArgumentException">The reference does not designate a GitHub repository.</exception>
  /// <exception cref="BadGatewayException">GitHub refused the reading, does not answer or answered with an unreadable payload.</exception>
  /// <exception cref="GitHubRateLimitedException">GitHub rate limited the request.</exception>
  public Task<ForgeCommitInfo?> GetCommitAsync(ForgeRepositoryRef repository, GitSha sha, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(repository);
    ArgumentNullException.ThrowIfNull(sha);

    var (installationId, root) = Locate(repository);

    return GetAsync(
      installationId,
      $"{root}/commits/{sha.Value}",
      GitHubJsonContext.Default.GitHubCommitResponse,
      NoSuchGitObject,
      GitHubGatewayMapper.ToForgeCommit,
      cancellationToken
    );
  }

  /// <inheritdoc />
  /// <exception cref="ArgumentException">The reference does not designate a GitHub repository.</exception>
  /// <exception cref="BadGatewayException">GitHub refused the reading, does not answer or answered with an unreadable payload.</exception>
  /// <exception cref="GitHubRateLimitedException">GitHub rate limited the request.</exception>
  public Task<ForgeCompareResult?> CompareAsync(
    ForgeRepositoryRef repository,
    GitSha baseSha,
    GitSha headSha,
    CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(repository);
    ArgumentNullException.ThrowIfNull(baseSha);
    ArgumentNullException.ThrowIfNull(headSha);

    var (installationId, root) = Locate(repository);

    return GetAsync(
      installationId,
      $"{root}/compare/{baseSha.Value}...{headSha.Value}",
      GitHubJsonContext.Default.GitHubCompareResponse,
      NoSuchGitObject,
      GitHubGatewayMapper.ToForgeCompare,
      cancellationToken
    );
  }

  /// <inheritdoc />
  /// <exception cref="ArgumentException">The reference does not designate a GitHub repository.</exception>
  /// <exception cref="BadGatewayException">GitHub refused the reading, does not answer or answered with an unreadable payload.</exception>
  /// <exception cref="GitHubRateLimitedException">GitHub rate limited the request.</exception>
  public Task<ForgePullInfo?> GetPullAsync(ForgeRepositoryRef repository, int number, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(repository);

    var (installationId, root) = Locate(repository);

    return GetAsync(
      installationId,
      string.Create(CultureInfo.InvariantCulture, $"{root}/pulls/{number}"),
      GitHubJsonContext.Default.GitHubPullResponse,
      NotFound,
      GitHubGatewayMapper.ToForgePull,
      cancellationToken
    );
  }

  /// <inheritdoc />
  /// <exception cref="BadGatewayException">GitHub refused the conversion, does not answer or answered with an unreadable payload.</exception>
  /// <exception cref="GitHubRateLimitedException">GitHub rate limited the request.</exception>
  /// <remarks>The conversion needs no authentication: the code, valid one hour and once, is the proof.</remarks>
  public async Task<GitHubAppCredentials?> ConvertManifestAsync(string code, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(code);

    var address = $"app-manifests/{Uri.EscapeDataString(code)}/conversions";

    using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(address, UriKind.Relative));
    using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);

    return await ReadAsync(
             response,
             "app-manifests/{code}/conversions",
             GitHubJsonContext.Default.GitHubManifestConversionResponse,
             ManifestCodeRefused,
             static app => new GitHubAppCredentials(
               app.Id.ToString(CultureInfo.InvariantCulture),
               app.Slug,
               app.ClientId,
               app.ClientSecret,
               app.WebhookSecret,
               app.Pem
             ),
             cancellationToken
           ).ConfigureAwait(false);
  }

  /// <inheritdoc />
  /// <remarks>
  ///   A refusal no retry can heal is permanent, so that the job fails at once and the sweep does not force the commit
  ///   again: a 404 (a repository or a commit GitHub does not know), a 403 that is not a rate limit (the App may not
  ///   write statuses, or the repository is archived), a 422 (a SHA GitHub does not know, or a commit that holds 1000
  ///   statuses already). A rate limit and a failure of GitHub stay transient.
  /// </remarks>
  /// <exception cref="ArgumentException">The reference does not designate a GitHub repository.</exception>
  /// <exception cref="NotFoundException">GitHub does not know the repository or the commit (GitHub.StatusRefused).</exception>
  /// <exception cref="UnprocessableEntityException">GitHub refuses the status for good (GitHub.StatusRefused).</exception>
  /// <exception cref="BadGatewayException">GitHub refused the status for another reason, or does not answer.</exception>
  /// <exception cref="GitHubRateLimitedException">GitHub rate limited the request.</exception>
  public async Task CreateStatusAsync(ForgeRepositoryRef repository, GitSha sha, CommitStatus status, string? targetUrl, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(repository);
    ArgumentNullException.ThrowIfNull(sha);
    ArgumentNullException.ThrowIfNull(status);

    var (installationId, root) = Locate(repository);
    var address = $"{root}/statuses/{sha.Value}";
    var state = status.State switch
    {
      StatusState.Success => "success",
      StatusState.Failure => "failure",
      var _ => "error",
    };

    using var request = await CreateRequestAsync(installationId, address, cancellationToken).ConfigureAwait(false);

    request.Method = HttpMethod.Post;
    // A status sent twice replaces itself.
    request.Options.Set(GitHubHttpClientConfiguration.Repeatable, true);
    request.Content = JsonContent.Create(
      new GitHubStatusRequest(state, status.Context, status.Description, targetUrl),
      GitHubJsonContext.Default.GitHubStatusRequest
    );

    using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);

    // The rate limits and the failures of GitHub were thrown by the pipeline: what is left is an answer about this
    // status, and these ones stay the same however often it is sent again.
    var refused = string.Create(CultureInfo.InvariantCulture, $"GitHub refused the status '{status.Context}' on '{address}': HTTP {(int)response.StatusCode}.");

    switch (response.StatusCode)
    {
      case HttpStatusCode.NotFound:
        throw new NotFoundException(GitHubErrors.StatusRefused, refused);
      case HttpStatusCode.Forbidden or HttpStatusCode.UnprocessableEntity:
        throw new UnprocessableEntityException(GitHubErrors.StatusRefused, refused);
    }

    // Nothing is absent here: any other refusal is a failure of the publication, tried again.
    _ = IsAbsent(response, address, []);
  }

  /// <inheritdoc />
  /// <exception cref="ArgumentException">The reference does not designate a GitHub repository, or the name is empty.</exception>
  /// <exception cref="BadGatewayException">GitHub refused the reading, does not answer or answered with an unreadable payload.</exception>
  /// <exception cref="GitHubRateLimitedException">GitHub rate limited the request.</exception>
  public async Task<GitSha?> GetBranchHeadAsync(ForgeRepositoryRef repository, string branch, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(repository);
    ArgumentException.ThrowIfNullOrWhiteSpace(branch);

    var (installationId, root) = Locate(repository);

    // Each part of the name is escaped; its slashes stay, as GitHub reads them.
    var address = $"{root}/branches/{string.Join('/', branch.Split('/').Select(Uri.EscapeDataString))}";
    var read = await GetAsync(installationId, address, GitHubJsonContext.Default.GitHubBranchResponse, NotFound, static response => response, cancellationToken)
                 .ConfigureAwait(false);

    if (read is null)
    {
      return null;
    }

    return GitSha.TryParse(read.Commit?.Sha, out var head)
             ? head
             : throw new BadGatewayException(GitHubErrors.Unavailable, $"GitHub answered the reading of '{address}' without the commit of the branch.");
  }

  /// <inheritdoc />
  /// <remarks>
  ///   A 404 is not a member. A 403 that says the App may not read the members (the permission
  ///   <c>Organization &gt; Members: read</c>) is read as not a member too, with a warning: the user takes part and
  ///   never administers. Any other 403 is a refusal of this organization (<see cref="ForbiddenException" />), for
  ///   instance its SAML enforcement; any other failure is a failure of GitHub.
  /// </remarks>
  /// <exception cref="ForbiddenException">GitHub refuses to tell the membership in this organization.</exception>
  /// <exception cref="BadGatewayException">GitHub refused the reading for another reason, does not answer or answered with an unreadable payload.</exception>
  /// <exception cref="GitHubRateLimitedException">GitHub rate limited the request.</exception>
  public async Task<GitHubOrganizationMembership?> GetOrganizationMembershipAsync(
    long installationId,
    string organizationLogin,
    string userLogin,
    CancellationToken cancellationToken)
  {
    ArgumentOutOfRangeException.ThrowIfNegativeOrZero(installationId);
    ArgumentException.ThrowIfNullOrWhiteSpace(organizationLogin);
    ArgumentException.ThrowIfNullOrWhiteSpace(userLogin);

    var address = $"orgs/{Uri.EscapeDataString(organizationLogin)}/memberships/{Uri.EscapeDataString(userLogin)}";

    using var request = await CreateRequestAsync(installationId, address, cancellationToken).ConfigureAwait(false);
    using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);

    if (response.StatusCode is HttpStatusCode.Forbidden)
    {
      if (!await IsNotAccessibleByIntegrationAsync(response, cancellationToken).ConfigureAwait(false))
      {
        throw new ForbiddenException(SecurityErrors.Forbidden, $"GitHub refused the reading of '{address}': HTTP 403.");
      }

      LogMembersNotReadable(logger, organizationLogin, installationId);

      return null;
    }

    // Only an active membership with the admin role administers: an invitation that was not accepted grants nothing.
    return await ReadAsync(
             response,
             address,
             GitHubJsonContext.Default.GitHubOrganizationMembershipResponse,
             NotFound,
             static membership => new GitHubOrganizationMembership(
               string.Equals(membership.State, "active", StringComparison.Ordinal),
               string.Equals(membership.Role, "admin", StringComparison.Ordinal),
               membership.User?.Id?.ToString(CultureInfo.InvariantCulture)
             ),
             cancellationToken
           ).ConfigureAwait(false);
  }

  /// <summary>Tells whether a 403 says that the App lacks the permission to read a resource.</summary>
  /// <param name="response">The response.</param>
  /// <param name="cancellationToken">A token to cancel the reading.</param>
  /// <returns><see langword="true" /> for the message <c>Resource not accessible by integration</c>.</returns>
  private static async Task<bool> IsNotAccessibleByIntegrationAsync(HttpResponseMessage response, CancellationToken cancellationToken)
  {
    try
    {
      var body = await response.Content.ReadFromJsonAsync(GitHubJsonContext.Default.GitHubErrorResponse, cancellationToken).ConfigureAwait(false);

      return string.Equals(body?.Message, "Resource not accessible by integration", StringComparison.Ordinal);
    }
    catch (JsonException)
    {
      return false;
    }
  }

  [LoggerMessage(
    EventId = 1500,
    Level = LogLevel.Warning,
    Message = "The GitHub App may not read the members of {Organization} (installation {InstallationId}): its owners are participants until the permission Organization > Members: read is granted"
  )]
  private static partial void LogMembersNotReadable(ILogger logger, string organization, long installationId);

  /// <inheritdoc />
  /// <remarks>
  ///   A comment was written by this application when its author is the bot account of the App,
  ///   <c>{slug}[bot]</c>. Until the slug of the App is known (<c>GitHub:AppSlug</c>), no comment is.
  /// </remarks>
  /// <exception cref="ArgumentException">The reference does not designate a GitHub repository.</exception>
  /// <exception cref="BadGatewayException">
  ///   GitHub refused the reading, does not answer, answered with an unreadable payload, or the pull request has more
  ///   comments than this instance reads.
  /// </exception>
  /// <exception cref="GitHubRateLimitedException">GitHub rate limited the request.</exception>
  public async Task<IReadOnlyList<ForgeIssueComment>> ListIssueCommentsAsync(ForgeRepositoryRef repository, int number, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(repository);
    ArgumentOutOfRangeException.ThrowIfNegativeOrZero(number);

    var (installationId, root) = Locate(repository);
    var author = options.CurrentValue.AppSlug is { Length: > 0 } slug ? $"{slug}[bot]" : null;
    var comments = new List<ForgeIssueComment>();

    for (var page = 1; page <= CommentsMaxPages; page++)
    {
      var read = await GetAsync<IReadOnlyList<GitHubIssueCommentResponse>, IReadOnlyList<ForgeIssueComment>>(
                   installationId,
                   string.Create(CultureInfo.InvariantCulture, $"{root}/issues/{number}/comments?per_page={ListPerPage}&page={page}"),
                   GitHubJsonContext.Default.IReadOnlyListGitHubIssueCommentResponse,
                   NotFound,
                   list =>
                   [
                     .. list.Select(comment => new ForgeIssueComment(
                                      comment.Id,
                                      comment.Body ?? string.Empty,
                                      author is not null && string.Equals(comment.User?.Login, author, StringComparison.OrdinalIgnoreCase)
                                    )
                     ),
                   ],
                   cancellationToken
                 ).ConfigureAwait(false);

      if (read is null)
      {
        return [];
      }

      comments.AddRange(read);

      if (read.Count < ListPerPage)
      {
        return comments;
      }
    }

    // A partial listing could miss the comment of this application, and a second one would be written.
    throw new BadGatewayException(GitHubErrors.Unavailable, "The pull request has more comments than this instance reads.");
  }

  /// <inheritdoc />
  /// <exception cref="ArgumentException">The reference does not designate a GitHub repository.</exception>
  /// <exception cref="BadGatewayException">GitHub refused the comment for another reason, does not answer or answered with an unreadable payload.</exception>
  /// <exception cref="GitHubRateLimitedException">GitHub rate limited the request.</exception>
  public async Task<long?> CreateIssueCommentAsync(ForgeRepositoryRef repository, int number, string body, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(repository);
    ArgumentNullException.ThrowIfNull(body);
    ArgumentOutOfRangeException.ThrowIfNegativeOrZero(number);

    var (installationId, root) = Locate(repository);
    var address = string.Create(CultureInfo.InvariantCulture, $"{root}/issues/{number}/comments");

    using var request = await CreateRequestAsync(installationId, address, cancellationToken).ConfigureAwait(false);

    request.Method = HttpMethod.Post;
    request.Content = JsonContent.Create(new GitHubIssueCommentRequest(body), GitHubJsonContext.Default.GitHubIssueCommentRequest);

    using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);

    var created = await ReadAsync(
                    response,
                    address,
                    GitHubJsonContext.Default.GitHubIssueCommentResponse,
                    CommentRefused,
                    static comment => comment,
                    cancellationToken
                  ).ConfigureAwait(false);

    return created?.Id;
  }

  /// <inheritdoc />
  /// <exception cref="ArgumentException">The reference does not designate a GitHub repository.</exception>
  /// <exception cref="BadGatewayException">GitHub refused the update for another reason or does not answer.</exception>
  /// <exception cref="GitHubRateLimitedException">GitHub rate limited the request.</exception>
  public async Task<bool> UpdateIssueCommentAsync(ForgeRepositoryRef repository, long commentId, string body, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(repository);
    ArgumentNullException.ThrowIfNull(body);
    ArgumentOutOfRangeException.ThrowIfNegativeOrZero(commentId);

    var (installationId, root) = Locate(repository);
    var address = string.Create(CultureInfo.InvariantCulture, $"{root}/issues/comments/{commentId}");

    using var request = await CreateRequestAsync(installationId, address, cancellationToken).ConfigureAwait(false);

    request.Method = HttpMethod.Patch;
    request.Content = JsonContent.Create(new GitHubIssueCommentRequest(body), GitHubJsonContext.Default.GitHubIssueCommentRequest);

    using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);

    return !IsAbsent(response, address, NotFound);
  }

  /// <inheritdoc />
  /// <exception cref="ArgumentException">The reference does not designate a GitHub repository.</exception>
  /// <exception cref="BadGatewayException">GitHub refused the deletion for another reason or does not answer.</exception>
  /// <exception cref="GitHubRateLimitedException">GitHub rate limited the request.</exception>
  public async Task<ForgeCommentDeletion> DeleteIssueCommentAsync(ForgeRepositoryRef repository, long commentId, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(repository);
    ArgumentOutOfRangeException.ThrowIfNegativeOrZero(commentId);

    var (installationId, root) = Locate(repository);
    var address = string.Create(CultureInfo.InvariantCulture, $"{root}/issues/comments/{commentId}");

    using var request = await CreateRequestAsync(installationId, address, cancellationToken).ConfigureAwait(false);

    request.Method = HttpMethod.Delete;

    using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);

    // A refusal that is not a rate limit (the RateLimitHandler raised those already): the comment stays.
    if (response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.UnprocessableEntity)
    {
      return ForgeCommentDeletion.Refused;
    }

    return IsAbsent(response, address, NotFound) ? ForgeCommentDeletion.Absent : ForgeCommentDeletion.Deleted;
  }

  /// <inheritdoc />
  /// <exception cref="BadGatewayException">GitHub refused the reading, does not answer or answered with an unreadable payload.</exception>
  /// <exception cref="GitHubRateLimitedException">GitHub rate limited the request.</exception>
  public async Task<IReadOnlyList<GitHubInstallationInfo>> ListInstallationsAsync(CancellationToken cancellationToken)
  {
    var installations = new List<GitHubInstallationInfo>();

    for (var page = 1; page <= ListMaxPages; page++)
    {
      var address = string.Create(CultureInfo.InvariantCulture, $"app/installations?per_page={ListPerPage}&page={page}");

      using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(address, UriKind.Relative));

      request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokens.CreateAppJwt());

      using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);

      var read = await ReadAsync<IReadOnlyList<GitHubInstallationListItem>, IReadOnlyList<GitHubInstallationInfo>>(
                   response,
                   address,
                   GitHubJsonContext.Default.IReadOnlyListGitHubInstallationListItem,
                   [],
                   static list =>
                   [
                     .. list.Select(static i => new GitHubInstallationInfo(
                                      i.Id,
                                      i.Account.Id.ToString(CultureInfo.InvariantCulture),
                                      i.Account.Login ?? string.Empty,
                                      GitHubAccountTypes.ToAccountType(i.Account.Type),
                                      i.SuspendedAt is not null
                                    )
                     ),
                   ],
                   cancellationToken
                 ).ConfigureAwait(false);

      installations.AddRange(read!);

      if (read!.Count < ListPerPage)
      {
        return installations;
      }
    }

    throw new BadGatewayException(GitHubErrors.Unavailable, "GitHub lists more installations than this instance reads.");
  }

  /// <inheritdoc />
  /// <remarks>Authenticated as the App itself: an installation that is suspended or deleted has no token.</remarks>
  /// <exception cref="BadGatewayException">GitHub refused the reading, does not answer or answered with an unreadable payload.</exception>
  /// <exception cref="GitHubRateLimitedException">GitHub rate limited the request.</exception>
  public async Task<GitHubInstallationInfo?> GetInstallationAsync(long installationId, CancellationToken cancellationToken)
  {
    ArgumentOutOfRangeException.ThrowIfNegativeOrZero(installationId);

    var address = string.Create(CultureInfo.InvariantCulture, $"app/installations/{installationId}");

    using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(address, UriKind.Relative));

    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokens.CreateAppJwt());

    using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);

    return await ReadAsync(
             response,
             address,
             GitHubJsonContext.Default.GitHubInstallationListItem,
             NotFound,
             static installation => new GitHubInstallationInfo(
               installation.Id,
               installation.Account.Id.ToString(CultureInfo.InvariantCulture),
               installation.Account.Login ?? string.Empty,
               GitHubAccountTypes.ToAccountType(installation.Account.Type),
               installation.SuspendedAt is not null
             ),
             cancellationToken
           ).ConfigureAwait(false);
  }

  /// <inheritdoc />
  /// <exception cref="BadGatewayException">GitHub refused the reading, does not answer or answered with an unreadable payload.</exception>
  /// <exception cref="GitHubRateLimitedException">GitHub rate limited the request.</exception>
  public async Task<IReadOnlyList<ForgeRepositoryInfo>> ListInstallationRepositoriesAsync(long installationId, CancellationToken cancellationToken)
  {
    ArgumentOutOfRangeException.ThrowIfNegativeOrZero(installationId);

    var repositories = new List<ForgeRepositoryInfo>();

    for (var page = 1; page <= ListMaxPages; page++)
    {
      var read = await GetAsync(
                   installationId,
                   string.Create(CultureInfo.InvariantCulture, $"installation/repositories?per_page={ListPerPage}&page={page}"),
                   GitHubJsonContext.Default.GitHubInstallationRepositoriesResponse,
                   [],
                   static body => body,
                   cancellationToken
                 ).ConfigureAwait(false);

      repositories.AddRange(read!.Repositories.Select(GitHubGatewayMapper.ToForgeRepository));

      if (read.Repositories.Count < ListPerPage)
      {
        // The pages are numbered: a repository added or removed between two readings shifts them, and one is
        // then read twice or not at all. A list that does not hold what GitHub counted is read again later.
        if (repositories.Count != read.TotalCount || repositories.DistinctBy(static r => r.ExternalId).Count() != repositories.Count)
        {
          throw new BadGatewayException(
            GitHubErrors.Unavailable,
            string.Create(
              CultureInfo.InvariantCulture,
              $"The repositories of installation {installationId} changed while they were listed: {repositories.Count} read, {read.TotalCount} counted."
            )
          );
        }

        return repositories;
      }
    }

    throw new BadGatewayException(GitHubErrors.Unavailable, "GitHub lists more repositories than this instance reads for one installation.");
  }

  /// <inheritdoc />
  /// <exception cref="ArgumentException">The reference does not designate a GitHub repository.</exception>
  /// <exception cref="BadGatewayException">GitHub refused the reading, does not answer or answered with an unreadable payload.</exception>
  /// <exception cref="GitHubRateLimitedException">GitHub rate limited the request.</exception>
  /// <remarks>A last page that is full leaves the list marked truncated: GitHub cannot say whether a 3001st file exists.</remarks>
  public async Task<ForgePullFiles?> ListPullFilesAsync(ForgeRepositoryRef repository, int number, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(repository);
    ArgumentOutOfRangeException.ThrowIfNegativeOrZero(number);

    var (installationId, root) = Locate(repository);
    var files = new List<ForgeChangedFile>();

    for (var page = 1; page <= PullFilesMaxPages; page++)
    {
      var read = await GetAsync<IReadOnlyList<GitHubCompareFile>, IReadOnlyList<ForgeChangedFile>>(
                   installationId,
                   string.Create(CultureInfo.InvariantCulture, $"{root}/pulls/{number}/files?per_page={PullFilesPerPage}&page={page}"),
                   GitHubJsonContext.Default.IReadOnlyListGitHubCompareFile,
                   NotFound,
                   static list => [.. list.Select(GitHubGatewayMapper.ToForgeChangedFile)],
                   cancellationToken
                 ).ConfigureAwait(false);

      if (read is null)
      {
        return null;
      }

      files.AddRange(read);

      if (read.Count < PullFilesPerPage)
      {
        return new ForgePullFiles(files, Truncated: false);
      }
    }

    return new ForgePullFiles(files, Truncated: true);
  }

  /// <inheritdoc />
  /// <exception cref="ArgumentException">The reference does not designate a GitHub repository.</exception>
  /// <exception cref="BadGatewayException">GitHub refused the reading, does not answer or answered with an unreadable payload.</exception>
  /// <exception cref="GitHubRateLimitedException">GitHub rate limited the request.</exception>
  public Task<ForgeTree?> GetTreeAsync(
    ForgeRepositoryRef repository,
    GitSha sha,
    bool recursive,
    CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(repository);
    ArgumentNullException.ThrowIfNull(sha);

    var (installationId, root) = Locate(repository);

    return GetAsync(
      installationId,
      $"{root}/git/trees/{sha.Value}{(recursive ? "?recursive=1" : string.Empty)}",
      GitHubJsonContext.Default.GitHubTreeResponse,
      NoSuchGitObject,
      tree => tree.ToForgeTree(sha),
      cancellationToken
    );
  }

  /// <inheritdoc />
  /// <exception cref="ArgumentException">The reference does not designate a GitHub repository.</exception>
  /// <exception cref="BadGatewayException">GitHub refused the reading or does not answer.</exception>
  /// <exception cref="GitHubRateLimitedException">GitHub rate limited the request.</exception>
  public async Task<string?> GetContentAsync(
    ForgeRepositoryRef repository,
    string path,
    GitSha sha,
    CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(repository);
    ArgumentNullException.ThrowIfNull(path);
    ArgumentNullException.ThrowIfNull(sha);

    var (installationId, root) = Locate(repository);
    var segments = path.Split('/');

    if (!Array.TrueForAll(segments, IsPathSegment))
    {
      return null;
    }

    var address = $"{root}/contents/{string.Join('/', segments.Select(Uri.EscapeDataString))}?ref={sha.Value}";

    using var request = await CreateRequestAsync(installationId, address, cancellationToken).ConfigureAwait(false);
    request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(RawMediaType));

    using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);

    return IsAbsent(response, address, NotFound)
             ? null
             : await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
  }

  /// <inheritdoc />
  /// <exception cref="ArgumentException">The reference does not designate a GitHub repository.</exception>
  /// <exception cref="BadGatewayException">GitHub refused the reading or does not answer.</exception>
  /// <exception cref="GitHubRateLimitedException">GitHub rate limited the request.</exception>
  public async Task<ForgeFileContent?> GetBoundedContentAsync(
    ForgeRepositoryRef repository,
    string path,
    GitSha sha,
    int maxBytes,
    CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(repository);
    ArgumentNullException.ThrowIfNull(path);
    ArgumentNullException.ThrowIfNull(sha);
    ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxBytes);

    var (installationId, root) = Locate(repository);
    var segments = path.Split('/');

    if (!Array.TrueForAll(segments, IsPathSegment))
    {
      return null;
    }

    var address = $"{root}/contents/{string.Join('/', segments.Select(Uri.EscapeDataString))}?ref={sha.Value}";

    using var request = await CreateRequestAsync(installationId, address, cancellationToken).ConfigureAwait(false);
    request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(RawMediaType));

    // Only the headers are awaited: the length they announce, then the body read up to the bound, decide.
    using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);

    if (IsAbsent(response, address, NotFound))
    {
      return null;
    }

    if (response.Content.Headers.ContentLength > maxBytes)
    {
      return ForgeFileContent.TooLarge;
    }

    var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);

    // One byte beyond the bound tells a file too large; an announced length, which the stream never exceeds, sizes the
    // buffer below the bound.
    var size = (int)Math.Min((response.Content.Headers.ContentLength ?? maxBytes) + 1, maxBytes + 1L);
    var buffer = ArrayPool<byte>.Shared.Rent(size);

    try
    {
      await using (stream.ConfigureAwait(false))
      {
        var length = 0;
        int read;

        while (length < size && (read = await stream.ReadAsync(buffer.AsMemory(length, size - length), cancellationToken).ConfigureAwait(false)) > 0)
        {
          length += read;
        }

        return length > maxBytes ? ForgeFileContent.TooLarge : new ForgeFileContent(Encoding.UTF8.GetString(buffer, 0, length));
      }
    }
    finally
    {
      ArrayPool<byte>.Shared.Return(buffer);
    }
  }

  /// <summary>Tells whether a text can stand for one level of an address without moving to another one.</summary>
  /// <param name="value">The text, which comes from outside.</param>
  /// <returns>
  ///   <see langword="false" /> when empty, or for <c>.</c> and <c>..</c>, which no git object or GitHub account is
  ///   named and which would otherwise lead the request to another endpoint of the API.
  /// </returns>
  private static bool IsPathSegment(string value) => value is not ("" or "." or "..");

  /// <summary>Reads the address of a repository.</summary>
  /// <param name="repository">The repository.</param>
  /// <returns>The installation that serves it and the root of its routes.</returns>
  /// <exception cref="ArgumentException">The reference does not designate a GitHub repository.</exception>
  private static (long InstallationId, string Root) Locate(ForgeRepositoryRef repository)
  {
    ArgumentException.ThrowIf(repository.Forge is not Forge.GitHub, "The repository is not on GitHub.", nameof(repository));

    ArgumentException.ThrowIf(
      !long.TryParse(repository.ConnectionId, NumberStyles.None, CultureInfo.InvariantCulture, out var installationId),
      "The connection of a GitHub repository is the numeric identifier of an installation.",
      nameof(repository)
    );

    ArgumentException.ThrowIf(
      !long.TryParse(repository.ExternalId, NumberStyles.None, CultureInfo.InvariantCulture, out var repositoryId),
      "The identifier of a GitHub repository is numeric.",
      nameof(repository)
    );

    return (installationId, string.Create(CultureInfo.InvariantCulture, $"repositories/{repositoryId}"));
  }

  /// <summary>Tells whether a response says that what was asked for does not exist.</summary>
  /// <param name="response">The response.</param>
  /// <param name="address">The relative address that was read.</param>
  /// <param name="absent">The refusals that mean the absence of the resource.</param>
  /// <returns><see langword="true" /> for such a refusal; <see langword="false" /> for a success.</returns>
  /// <exception cref="BadGatewayException">GitHub refused the reading for another reason.</exception>
  private static bool IsAbsent(HttpResponseMessage response, string address, HttpStatusCode[] absent)
  {
    if (response.IsSuccessStatusCode)
    {
      return false;
    }

    if (Array.IndexOf(absent, response.StatusCode) >= 0)
    {
      return true;
    }

    throw new BadGatewayException(
      GitHubErrors.Unavailable,
      string.Create(CultureInfo.InvariantCulture, $"GitHub refused the reading of '{address}': HTTP {(int)response.StatusCode}.")
    );
  }

  /// <summary>Reads and converts the payload of a response.</summary>
  /// <typeparam name="TModel">The type of the payload.</typeparam>
  /// <typeparam name="TResult">The type of the result.</typeparam>
  /// <param name="response">The response.</param>
  /// <param name="address">The relative address that was read.</param>
  /// <param name="type">The serialization of the payload.</param>
  /// <param name="absent">The refusals that mean the absence of the resource.</param>
  /// <param name="map">The conversion of the payload.</param>
  /// <param name="cancellationToken">A token to cancel the reading.</param>
  /// <returns>The result, or <see langword="null" /> when the resource does not exist.</returns>
  /// <exception cref="BadGatewayException">GitHub refused the reading or answered with an unreadable payload.</exception>
  private static async Task<TResult?> ReadAsync<TModel, TResult>(
    HttpResponseMessage response,
    string address,
    JsonTypeInfo<TModel> type,
    HttpStatusCode[] absent,
    Func<TModel, TResult> map,
    CancellationToken cancellationToken)
    where TModel : class
    where TResult : class
  {
    if (IsAbsent(response, address, absent))
    {
      return null;
    }

    try
    {
      var model = await response.Content.ReadFromJsonAsync(type, cancellationToken).ConfigureAwait(false)
                  ?? throw new JsonException("The payload is empty.");

      return map(model);
    }
    catch (JsonException exception)
    {
      throw new BadGatewayException(
        GitHubErrors.Unavailable,
        $"GitHub answered the reading of '{address}' with an unreadable payload.",
        exception
      );
    }
  }

  /// <summary>Creates a reading authenticated by the token of an installation.</summary>
  /// <param name="installationId">The installation.</param>
  /// <param name="address">The relative address to read.</param>
  /// <param name="cancellationToken">A token to cancel the call.</param>
  /// <returns>The request, which also names the installation whose quota it consumes.</returns>
  private async Task<HttpRequestMessage> CreateRequestAsync(
    long installationId,
    string address,
    CancellationToken cancellationToken)
  {
    var token = await tokens.GetInstallationTokenAsync(installationId, cancellationToken).ConfigureAwait(false);

    var request = new HttpRequestMessage(HttpMethod.Get, new Uri(address, UriKind.Relative));
    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
    request.Options.Set(RateLimitHandler.InstallationId, installationId);

    return request;
  }

  /// <summary>Reads a resource with the token of an installation.</summary>
  /// <typeparam name="TModel">The type of the payload.</typeparam>
  /// <typeparam name="TResult">The type of the result.</typeparam>
  /// <param name="installationId">The installation.</param>
  /// <param name="address">The relative address to read.</param>
  /// <param name="type">The serialization of the payload.</param>
  /// <param name="absent">The refusals that mean the absence of the resource.</param>
  /// <param name="map">The conversion of the payload.</param>
  /// <param name="cancellationToken">A token to cancel the reading.</param>
  /// <returns>The result, or <see langword="null" /> when the resource does not exist.</returns>
  private async Task<TResult?> GetAsync<TModel, TResult>(
    long installationId,
    string address,
    JsonTypeInfo<TModel> type,
    HttpStatusCode[] absent,
    Func<TModel, TResult> map,
    CancellationToken cancellationToken)
    where TModel : class
    where TResult : class
  {
    using var request = await CreateRequestAsync(installationId, address, cancellationToken).ConfigureAwait(false);
    using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);

    return await ReadAsync(response, address, type, absent, map, cancellationToken).ConfigureAwait(false);
  }

  /// <summary>Finds the installation of a repository, authenticated as the App itself.</summary>
  /// <param name="address">The relative address to read.</param>
  /// <param name="cancellationToken">A token to cancel the reading.</param>
  /// <returns>The installation, or <see langword="null" /> when the App is not installed on the repository.</returns>
  private async Task<GitHubInstallationResponse?> ReadAsAppAsync(string address, CancellationToken cancellationToken)
  {
    using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(address, UriKind.Relative));
    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokens.CreateAppJwt());

    using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);

    return await ReadAsync(
             response,
             address,
             GitHubJsonContext.Default.GitHubInstallationResponse,
             NotFound,
             static installation => installation,
             cancellationToken
           ).ConfigureAwait(false);
  }

  #endregion
}
