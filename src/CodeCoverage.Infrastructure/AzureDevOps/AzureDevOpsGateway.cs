// ------------------------------------------------------------------------------------------------
// <copyright file="AzureDevOpsGateway.cs" company="Gaëtan Thouvenin">
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

using CodeCoverage.Application.Exceptions;
using CodeCoverage.Domain.Contracts.Forges;
using CodeCoverage.Domain.Git;
using CodeCoverage.Domain.Guards;
using CodeCoverage.Domain.Notifications;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Infrastructure.AzureDevOps.Models;
using CodeCoverage.Infrastructure.Forges;

namespace CodeCoverage.Infrastructure.AzureDevOps;

/// <summary>The readings of Azure DevOps, through its REST API and the Microsoft Entra ID application of the instance.</summary>
/// <param name="client">The <c>azure-devops</c> client, whose pipeline handles the rate limits and the failures of Azure DevOps.</param>
/// <param name="credentials">The reader of the connections of the organization of the scope.</param>
/// <param name="tokens">The tokens of the application, one per connection.</param>
/// <remarks>
///   <para>
///     A tracked repository is read by its identifier, a GUID, through <c>_apis/git/repositories/{id}</c> under the
///     address of its Azure DevOps organization, so neither a renaming of the repository nor one of its project breaks
///     a reading; only the search of a repository that is not tracked yet goes by project and name.
///   </para>
///   <para>
///     The base address of an Azure DevOps connection is the address of its organization, <c>{ApiBaseUrl}/{organization}/</c>:
///     every request is sent under it, and its last segment is the name of the organization. A refusal other than the
///     absence of what was asked for is an answer no caller can use: it surfaces as a <see cref="BadGatewayException" />.
///   </para>
///   <para>
///     The comparison and the files of a pull request (M7.46), the statuses (M7.47) and the comments (M7.48) are not
///     built yet: they are refused with a <see cref="NotSupportedException" />.
///   </para>
/// </remarks>
internal sealed class AzureDevOpsGateway(
  HttpClient client,
  ForgeConnectionCredentialReader credentials,
  AzureDevOpsTokenProvider tokens) : IForgeGateway
{
  #region Fields

  /// <summary>The version of the REST API every request asks for.</summary>
  public const string ApiVersion = "7.1";

  /// <summary>
  ///   The largest number of commits one listing reads. The listing of Azure DevOps leaves out the parents, so each
  ///   commit costs a reading of its own: a caller that needs more asks again from the last one.
  /// </summary>
  public const int CommitsMaxPerListing = 10;

  private static readonly HttpStatusCode[] NotFound = [HttpStatusCode.NotFound];

  #endregion

  #region Methods

  /// <inheritdoc />
  /// <exception cref="BadGatewayException">Azure DevOps refused the reading, does not answer or answered with an unreadable payload.</exception>
  /// <exception cref="ForgeRateLimitedException">Azure DevOps rate limited the request.</exception>
  /// <remarks>
  ///   The owner is <c>organization:project</c>. Each usable Azure DevOps connection of the organization of the scope
  ///   whose organization has that name is asked for the repository, by project and name; the first that has it serves
  ///   it.
  /// </remarks>
  public async Task<ForgeConnectionInfo?> GetRepositoryConnectionAsync(string owner, string name, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(owner);
    ArgumentNullException.ThrowIfNull(name);

    var separator = owner.IndexOf(':', StringComparison.Ordinal);

    if (separator <= 0)
    {
      return null;
    }

    var organization = owner[..separator];
    var project = owner[(separator + 1)..];

    if (!IsPathSegment(project) || !IsPathSegment(name))
    {
      return null;
    }

    var address = string.Create(
      CultureInfo.InvariantCulture,
      $"{Uri.EscapeDataString(project)}/_apis/git/repositories/{Uri.EscapeDataString(name)}?api-version={ApiVersion}"
    );

    foreach (var connection in await credentials.ListAsync(Forge.AzureDevOps, cancellationToken).ConfigureAwait(false))
    {
      if (!string.Equals(OrganizationOf(connection), organization, StringComparison.OrdinalIgnoreCase))
      {
        continue;
      }

      var found = await GetAsync(
                    connection,
                    address,
                    AzureDevOpsJsonContext.Default.AzureDevOpsRepository,
                    NotFound,
                    repository => new ForgeConnectionInfo(
                      connection.ConnectionId,
                      connection.ExternalAccountId,
                      repository.Id.ToString("D", CultureInfo.InvariantCulture),
                      Forge.AzureDevOps
                    ),
                    cancellationToken
                  ).ConfigureAwait(false);

      if (found is not null)
      {
        return found;
      }
    }

    return null;
  }

  /// <inheritdoc />
  /// <exception cref="ArgumentException">The reference does not designate an Azure DevOps repository.</exception>
  /// <exception cref="BadGatewayException">
  ///   The connection cannot be used, or Azure DevOps refused the reading, does not answer or answered with an unreadable
  ///   payload.
  /// </exception>
  /// <exception cref="ForgeRateLimitedException">Azure DevOps rate limited the request.</exception>
  public async Task<ForgeRepositoryInfo?> GetRepositoryAsync(ForgeRepositoryRef repository, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(repository);

    var root = Locate(repository);
    var connection = await credentials.ReadAsync(repository.ConnectionId, cancellationToken).ConfigureAwait(false);

    return await GetAsync(
             connection,
             $"{root}?api-version={ApiVersion}",
             AzureDevOpsJsonContext.Default.AzureDevOpsRepository,
             NotFound,
             read => read.ToForgeRepository(OrganizationOf(connection), connection.ExternalAccountId),
             cancellationToken
           ).ConfigureAwait(false);
  }

  /// <inheritdoc />
  /// <exception cref="ArgumentException">The reference does not designate an Azure DevOps repository.</exception>
  /// <exception cref="BadGatewayException">
  ///   The connection cannot be used, or Azure DevOps refused the reading, does not answer or answered with an unreadable
  ///   payload.
  /// </exception>
  /// <exception cref="ForgeRateLimitedException">Azure DevOps rate limited the request.</exception>
  /// <remarks>
  ///   The listing of Azure DevOps reports neither the parents of a commit nor its whole message, so the commits are
  ///   read one by one along the first parents, at most <paramref name="perPage" /> and
  ///   <see cref="CommitsMaxPerListing" /> of them: a commit the forge does not have ends the listing.
  /// </remarks>
  public async Task<IReadOnlyList<ForgeCommitInfo>> ListCommitsAsync(
    ForgeRepositoryRef repository,
    GitSha sha,
    int perPage,
    CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(repository);
    ArgumentNullException.ThrowIfNull(sha);
    ArgumentOutOfRangeException.ThrowIfNegativeOrZero(perPage);

    var root = Locate(repository);
    var connection = await credentials.ReadAsync(repository.ConnectionId, cancellationToken).ConfigureAwait(false);
    var limit = Math.Min(perPage, CommitsMaxPerListing);
    var commits = new List<ForgeCommitInfo>(limit);
    GitSha? next = sha;

    while (next is not null && commits.Count < limit)
    {
      var commit = await ReadCommitAsync(connection, root, next, cancellationToken).ConfigureAwait(false);

      if (commit is null)
      {
        break;
      }

      commits.Add(commit);
      next = commit.ParentShas.Count > 0 ? commit.ParentShas[0] : null;
    }

    return commits;
  }

  /// <inheritdoc />
  /// <exception cref="ArgumentException">The reference does not designate an Azure DevOps repository.</exception>
  /// <exception cref="BadGatewayException">
  ///   The connection cannot be used, or Azure DevOps refused the reading, does not answer or answered with an unreadable
  ///   payload.
  /// </exception>
  /// <exception cref="ForgeRateLimitedException">Azure DevOps rate limited the request.</exception>
  public async Task<ForgeCommitInfo?> GetCommitAsync(ForgeRepositoryRef repository, GitSha sha, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(repository);
    ArgumentNullException.ThrowIfNull(sha);

    var root = Locate(repository);
    var connection = await credentials.ReadAsync(repository.ConnectionId, cancellationToken).ConfigureAwait(false);

    return await ReadCommitAsync(connection, root, sha, cancellationToken).ConfigureAwait(false);
  }

  /// <inheritdoc />
  /// <exception cref="NotSupportedException">The comparison of Azure DevOps is not built yet (M7.46).</exception>
  public Task<ForgeCompareResult?> CompareAsync(
    ForgeRepositoryRef repository,
    GitSha baseSha,
    GitSha headSha,
    CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(repository);
    ArgumentNullException.ThrowIfNull(baseSha);
    ArgumentNullException.ThrowIfNull(headSha);

    return NotBuiltYet<ForgeCompareResult?>("comparison", "M7.46");
  }

  /// <inheritdoc />
  /// <exception cref="ArgumentException">The reference does not designate an Azure DevOps repository.</exception>
  /// <exception cref="BadGatewayException">
  ///   The connection cannot be used, or Azure DevOps refused the reading, does not answer or answered with an unreadable
  ///   payload.
  /// </exception>
  /// <exception cref="ForgeRateLimitedException">Azure DevOps rate limited the request.</exception>
  /// <remarks>
  ///   The head is <c>lastMergeSourceCommit</c>, the head of the source branch, and the base
  ///   <c>lastMergeTargetCommit</c>: never the merge commit Azure DevOps computes (critical rule 2).
  /// </remarks>
  public async Task<ForgePullInfo?> GetPullAsync(ForgeRepositoryRef repository, int number, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(repository);

    var root = Locate(repository);
    var connection = await credentials.ReadAsync(repository.ConnectionId, cancellationToken).ConfigureAwait(false);

    return await GetAsync(
             connection,
             string.Create(CultureInfo.InvariantCulture, $"{root}/pullrequests/{number}?api-version={ApiVersion}"),
             AzureDevOpsJsonContext.Default.AzureDevOpsPullRequest,
             NotFound,
             AzureDevOpsGatewayMapper.ToForgePull,
             cancellationToken
           ).ConfigureAwait(false);
  }

  /// <inheritdoc />
  /// <exception cref="ArgumentException">The reference does not designate an Azure DevOps repository, or the name is empty.</exception>
  /// <exception cref="BadGatewayException">
  ///   The connection cannot be used, or Azure DevOps refused the reading, does not answer or answered with an unreadable
  ///   payload.
  /// </exception>
  /// <exception cref="ForgeRateLimitedException">Azure DevOps rate limited the request.</exception>
  public async Task<GitSha?> GetBranchHeadAsync(ForgeRepositoryRef repository, string branch, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(repository);
    ArgumentException.ThrowIfNullOrWhiteSpace(branch);

    var root = Locate(repository);
    var connection = await credentials.ReadAsync(repository.ConnectionId, cancellationToken).ConfigureAwait(false);
    var refs = await GetAsync(
                 connection,
                 $"{root}/refs?filter={Uri.EscapeDataString("heads/" + branch)}&api-version={ApiVersion}",
                 AzureDevOpsJsonContext.Default.AzureDevOpsListAzureDevOpsRef,
                 NotFound,
                 static list => list,
                 cancellationToken
               ).ConfigureAwait(false);

    return refs?.ToBranchHead(branch);
  }

  /// <inheritdoc />
  /// <exception cref="NotSupportedException">The files of a pull request of Azure DevOps are not built yet (M7.46).</exception>
  public Task<ForgePullFiles?> ListPullFilesAsync(ForgeRepositoryRef repository, int number, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(repository);

    return NotBuiltYet<ForgePullFiles?>("files of a pull request", "M7.46");
  }

  /// <inheritdoc />
  /// <exception cref="ArgumentException">The reference does not designate an Azure DevOps repository.</exception>
  /// <exception cref="BadGatewayException">
  ///   The connection cannot be used, or Azure DevOps refused the reading, does not answer or answered with an unreadable
  ///   payload.
  /// </exception>
  /// <exception cref="ForgeRateLimitedException">Azure DevOps rate limited the request.</exception>
  /// <remarks>
  ///   Unlike GitHub, the trees of Azure DevOps are read by the SHA of a tree only: the SHA is first read as a commit,
  ///   whose root tree is then listed; a SHA that is not a commit is listed as the tree of a directory, the SHA
  ///   <see cref="ForgeTreeEntry.Sha" /> gives to one. The paths of a recursive reading are relative to the tree read.
  /// </remarks>
  public async Task<ForgeTree?> GetTreeAsync(ForgeRepositoryRef repository, GitSha sha, bool recursive, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(repository);
    ArgumentNullException.ThrowIfNull(sha);

    var root = Locate(repository);
    var connection = await credentials.ReadAsync(repository.ConnectionId, cancellationToken).ConfigureAwait(false);
    var treeSha = await GetAsync(
                    connection,
                    $"{root}/commits/{sha.Value}?api-version={ApiVersion}",
                    AzureDevOpsJsonContext.Default.AzureDevOpsCommit,
                    NotFound,
                    AzureDevOpsGatewayMapper.ToTreeSha,
                    cancellationToken
                  ).ConfigureAwait(false)
                  ?? sha;

    return await GetAsync(
             connection,
             $"{root}/trees/{treeSha.Value}?recursive={(recursive ? "true" : "false")}&api-version={ApiVersion}",
             AzureDevOpsJsonContext.Default.AzureDevOpsTree,
             NotFound,
             tree => tree.ToForgeTree(sha),
             cancellationToken
           ).ConfigureAwait(false);
  }

  /// <inheritdoc />
  /// <exception cref="NotSupportedException">The statuses of Azure DevOps are not built yet (M7.47).</exception>
  public Task CreateStatusAsync(ForgeRepositoryRef repository, GitSha sha, CommitStatus status, string? targetUrl, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(repository);
    ArgumentNullException.ThrowIfNull(sha);
    ArgumentNullException.ThrowIfNull(status);

    return NotBuiltYet<object?>("status", "M7.47");
  }

  /// <inheritdoc />
  /// <exception cref="NotSupportedException">The comments of Azure DevOps are not built yet (M7.48).</exception>
  public Task<IReadOnlyList<ForgeIssueComment>> ListIssueCommentsAsync(ForgeRepositoryRef repository, int number, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(repository);

    return NotBuiltYet<IReadOnlyList<ForgeIssueComment>>("comments", "M7.48");
  }

  /// <inheritdoc />
  /// <exception cref="NotSupportedException">The comments of Azure DevOps are not built yet (M7.48).</exception>
  public Task<long?> CreateIssueCommentAsync(ForgeRepositoryRef repository, int number, string body, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(repository);
    ArgumentNullException.ThrowIfNull(body);

    return NotBuiltYet<long?>("comments", "M7.48");
  }

  /// <inheritdoc />
  /// <exception cref="NotSupportedException">The comments of Azure DevOps are not built yet (M7.48).</exception>
  public Task<bool> UpdateIssueCommentAsync(ForgeRepositoryRef repository, long commentId, string body, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(repository);
    ArgumentNullException.ThrowIfNull(body);

    return NotBuiltYet<bool>("comments", "M7.48");
  }

  /// <inheritdoc />
  /// <exception cref="NotSupportedException">The comments of Azure DevOps are not built yet (M7.48).</exception>
  public Task<ForgeCommentDeletion> DeleteIssueCommentAsync(ForgeRepositoryRef repository, long commentId, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(repository);

    return NotBuiltYet<ForgeCommentDeletion>("comments", "M7.48");
  }

  /// <inheritdoc />
  /// <exception cref="ArgumentException">The reference does not designate an Azure DevOps repository.</exception>
  /// <exception cref="BadGatewayException">The connection cannot be used, or Azure DevOps refused the reading or does not answer.</exception>
  /// <exception cref="ForgeRateLimitedException">Azure DevOps rate limited the request.</exception>
  /// <remarks>
  ///   The file is read raw through <c>items</c> at the commit (<c>versionDescriptor.versionType=commit</c>); a path
  ///   with an empty, <c>.</c> or <c>..</c> segment names no file and is answered <see langword="null" /> without asking.
  /// </remarks>
  public async Task<string?> GetContentAsync(ForgeRepositoryRef repository, string path, GitSha sha, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(repository);
    ArgumentNullException.ThrowIfNull(path);
    ArgumentNullException.ThrowIfNull(sha);

    var root = Locate(repository);

    if (ContentAddress(root, path, sha) is not { } address)
    {
      return null;
    }

    var connection = await credentials.ReadAsync(repository.ConnectionId, cancellationToken).ConfigureAwait(false);

    using var response = await SendAsync(connection, address, HttpCompletionOption.ResponseContentRead, cancellationToken).ConfigureAwait(false);

    return IsAbsent(response, address, NotFound)
             ? null
             : await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
  }

  /// <inheritdoc />
  /// <exception cref="ArgumentException">The reference does not designate an Azure DevOps repository.</exception>
  /// <exception cref="BadGatewayException">The connection cannot be used, or Azure DevOps refused the reading or does not answer.</exception>
  /// <exception cref="ForgeRateLimitedException">Azure DevOps rate limited the request.</exception>
  /// <remarks>
  ///   The file is read raw through <c>items</c> at the commit (<c>versionDescriptor.versionType=commit</c>): the length
  ///   announced by Azure DevOps is checked first, then the body is read up to the bound.
  /// </remarks>
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

    var root = Locate(repository);

    if (ContentAddress(root, path, sha) is not { } address)
    {
      return null;
    }

    var connection = await credentials.ReadAsync(repository.ConnectionId, cancellationToken).ConfigureAwait(false);

    // Only the headers are awaited: the length they announce, then the body read up to the bound, decide.
    using var response = await SendAsync(connection, address, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);

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

  /// <summary>Builds the address of the raw content of a file at a commit.</summary>
  /// <param name="root">The root of the routes of the repository.</param>
  /// <param name="path">The path of the file, relative to the root of the repository.</param>
  /// <param name="sha">The SHA of the commit.</param>
  /// <returns>
  ///   The address, or <see langword="null" /> when a segment of the path is empty, <c>.</c> or <c>..</c>: such a path
  ///   names no file.
  /// </returns>
  private static string? ContentAddress(string root, string path, GitSha sha) =>
    Array.TrueForAll(path.Split('/'), IsPathSegment)
      ? $"{root}/items?path={Uri.EscapeDataString("/" + path)}&versionDescriptor.version={sha.Value}"
        + $"&versionDescriptor.versionType=commit&$format=octetStream&api-version={ApiVersion}"
      : null;

  /// <summary>Tells whether a text can stand as one segment of an address.</summary>
  /// <param name="value">The text.</param>
  /// <returns>
  ///   <see langword="false" /> when empty, or for <c>.</c> and <c>..</c>, which no project or repository is named and
  ///   which would otherwise lead the request to another endpoint of the API.
  /// </returns>
  private static bool IsPathSegment(string value) => value is not ("" or "." or "..");

  /// <summary>Reads the address of a repository.</summary>
  /// <param name="repository">The repository.</param>
  /// <returns>The root of its routes, relative to the address of its organization, by its identifier.</returns>
  /// <exception cref="ArgumentException">The reference does not designate an Azure DevOps repository.</exception>
  private static string Locate(ForgeRepositoryRef repository)
  {
    ArgumentException.ThrowIf(repository.Forge is not Forge.AzureDevOps, "The repository is not on Azure DevOps.", nameof(repository));

    ArgumentException.ThrowIf(
      !Guid.TryParse(repository.ExternalId, out var repositoryId),
      "The identifier of an Azure DevOps repository is a GUID.",
      nameof(repository)
    );

    return string.Create(CultureInfo.InvariantCulture, $"_apis/git/repositories/{repositoryId:D}");
  }

  /// <summary>Refuses an operation of the port that the adapter does not build yet.</summary>
  /// <typeparam name="T">The type of the result of the operation.</typeparam>
  /// <param name="operation">What the operation reads or writes.</param>
  /// <param name="microFeature">The micro-feature that builds it.</param>
  /// <returns>A faulted task.</returns>
  private static Task<T> NotBuiltYet<T>(string operation, string microFeature) =>
    Task.FromException<T>(new NotSupportedException($"The {operation} of Azure DevOps is not built yet ({microFeature})."));

  /// <summary>Reads the name of the Azure DevOps organization of a connection.</summary>
  /// <param name="connection">The connection.</param>
  /// <returns>The last segment of its base address.</returns>
  private static string OrganizationOf(ForgeConnectionCredential connection) =>
    Uri.UnescapeDataString(connection.BaseAddress.AbsolutePath.TrimEnd('/').Split('/')[^1]);

  /// <summary>Gets the address of the organization of a connection, to which the routes are appended.</summary>
  /// <param name="connection">The connection.</param>
  /// <returns>Its base address, ending with a slash.</returns>
  private static Uri RootOf(ForgeConnectionCredential connection) =>
    new($"{connection.BaseAddress.AbsoluteUri.TrimEnd('/')}/", UriKind.Absolute);

  /// <summary>Tells whether a response says that what was asked for does not exist.</summary>
  /// <param name="response">The response.</param>
  /// <param name="address">The relative address that was read.</param>
  /// <param name="absent">The refusals that mean the absence of the resource.</param>
  /// <returns><see langword="true" /> for such a refusal; <see langword="false" /> for a success.</returns>
  /// <exception cref="BadGatewayException">Azure DevOps refused the reading for another reason.</exception>
  private static bool IsAbsent(HttpResponseMessage response, string address, HttpStatusCode[] absent)
  {
    if (response.IsSuccessStatusCode)
    {
      return false;
    }

    var isAbsent = Array.IndexOf(absent, response.StatusCode) >= 0;

    BadGatewayException.ThrowIfForgeRefused(
      !isAbsent,
      string.Create(CultureInfo.InvariantCulture, $"Azure DevOps refused the reading of '{address}': HTTP {(int)response.StatusCode}.")
    );

    return true;
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
  /// <exception cref="BadGatewayException">Azure DevOps refused the reading or answered with an unreadable payload.</exception>
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
      throw BadGatewayException.ForgeUnavailable($"Azure DevOps answered the reading of '{address}' with an unreadable payload.", exception);
    }
  }

  /// <summary>Reads a commit.</summary>
  /// <param name="connection">The connection of the repository.</param>
  /// <param name="root">The root of the routes of the repository.</param>
  /// <param name="sha">The SHA of the commit.</param>
  /// <param name="cancellationToken">A token to cancel the reading.</param>
  /// <returns>The commit, or <see langword="null" /> when the forge does not have it.</returns>
  private Task<ForgeCommitInfo?> ReadCommitAsync(
    ForgeConnectionCredential connection,
    string root,
    GitSha sha,
    CancellationToken cancellationToken) =>
    GetAsync(
      connection,
      $"{root}/commits/{sha.Value}?api-version={ApiVersion}",
      AzureDevOpsJsonContext.Default.AzureDevOpsCommit,
      NotFound,
      AzureDevOpsGatewayMapper.ToForgeCommit,
      cancellationToken
    );

  /// <summary>Reads a resource with the token of the application for a connection.</summary>
  /// <typeparam name="TModel">The type of the payload.</typeparam>
  /// <typeparam name="TResult">The type of the result.</typeparam>
  /// <param name="connection">The connection.</param>
  /// <param name="address">The address to read, relative to the address of the organization of the connection.</param>
  /// <param name="type">The serialization of the payload.</param>
  /// <param name="absent">The refusals that mean the absence of the resource.</param>
  /// <param name="map">The conversion of the payload.</param>
  /// <param name="cancellationToken">A token to cancel the reading.</param>
  /// <returns>The result, or <see langword="null" /> when the resource does not exist.</returns>
  private async Task<TResult?> GetAsync<TModel, TResult>(
    ForgeConnectionCredential connection,
    string address,
    JsonTypeInfo<TModel> type,
    HttpStatusCode[] absent,
    Func<TModel, TResult> map,
    CancellationToken cancellationToken)
    where TModel : class
    where TResult : class
  {
    using var response = await SendAsync(connection, address, HttpCompletionOption.ResponseContentRead, cancellationToken).ConfigureAwait(false);

    return await ReadAsync(response, address, type, absent, map, cancellationToken).ConfigureAwait(false);
  }

  /// <summary>Sends a reading with the token of the application for a connection.</summary>
  /// <param name="connection">The connection.</param>
  /// <param name="address">The address to read, relative to the address of the organization of the connection.</param>
  /// <param name="completion">When the sending completes: once the whole answer, or only its headers, are read.</param>
  /// <param name="cancellationToken">A token to cancel the reading.</param>
  /// <returns>The response, which the caller disposes of.</returns>
  private async Task<HttpResponseMessage> SendAsync(
    ForgeConnectionCredential connection,
    string address,
    HttpCompletionOption completion,
    CancellationToken cancellationToken)
  {
    var token = await tokens.GetTokenAsync(connection, cancellationToken).ConfigureAwait(false);

    using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(RootOf(connection), address));
    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

    return await client.SendAsync(request, completion, cancellationToken).ConfigureAwait(false);
  }

  #endregion
}
