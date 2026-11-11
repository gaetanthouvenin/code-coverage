// ------------------------------------------------------------------------------------------------
// <copyright file="GitHubUserGateway.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

using CodeCoverage.Application.Exceptions;
using CodeCoverage.Application.Security;
using CodeCoverage.Domain.Contracts.GitHub;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Domain.Users;
using CodeCoverage.Infrastructure.GitHub.Models;

namespace CodeCoverage.Infrastructure.GitHub;

/// <summary>Calls the GitHub API in the name of a user, with a user token.</summary>
/// <param name="client">The typed <c>github</c> HTTP client.</param>
/// <remarks>
///   A token GitHub refuses (HTTP 401) is an expired session: it is reported as unauthenticated, and the user signs
///   in again. Every other refusal is a failure of GitHub.
/// </remarks>
internal sealed class GitHubUserGateway(HttpClient client) : IGitHubUserGateway
{
  #region Fields

  private const int ListPerPage = 100;

  // 100 000 repositories in one installation: an end to a listing that never shrinks.
  private const int ListMaxPages = 1000;

  #endregion

  #region Methods

  /// <summary>Sends a reading with a user token and reads its answer.</summary>
  /// <typeparam name="TModel">The type of the payload.</typeparam>
  /// <param name="accessToken">The user token.</param>
  /// <param name="address">The address, relative to the API.</param>
  /// <param name="type">The JSON metadata of the payload.</param>
  /// <param name="absentWhenNotFound">Whether a 404 is an answer, given as <see langword="null" />.</param>
  /// <param name="cancellationToken">A token to cancel the reading.</param>
  /// <returns>The payload, or <see langword="null" /> for a 404 that is an answer.</returns>
  private async Task<TModel?> GetAsync<TModel>(
    string accessToken,
    string address,
    JsonTypeInfo<TModel> type,
    bool absentWhenNotFound,
    CancellationToken cancellationToken)
    where TModel : class
  {
    using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(address, UriKind.Relative));

    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
    request.Options.Set(RateLimitHandler.UserToken, true);

    using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);

    if (response.StatusCode is HttpStatusCode.Unauthorized)
    {
      throw new UnauthorizedException(SecurityErrors.Unauthenticated, "GitHub no longer accepts the token of the user.");
    }

    if (absentWhenNotFound && response.StatusCode is HttpStatusCode.NotFound)
    {
      return null;
    }

    if (!response.IsSuccessStatusCode)
    {
      throw new BadGatewayException(
        GitHubErrors.Unavailable,
        string.Create(CultureInfo.InvariantCulture, $"GitHub refused the reading of '{address}': HTTP {(int)response.StatusCode}.")
      );
    }

    try
    {
      return await response.Content.ReadFromJsonAsync(type, cancellationToken).ConfigureAwait(false)
             ?? throw new JsonException("The payload is empty.");
    }
    catch (JsonException exception)
    {
      throw new BadGatewayException(GitHubErrors.Unavailable, $"GitHub answered the reading of '{address}' with an unreadable payload.", exception);
    }
  }

  /// <summary>Gives the highest permission GitHub reports for a user on a repository.</summary>
  /// <param name="permissions">What GitHub reports, or <see langword="null" />.</param>
  /// <returns>The permission; reading when GitHub says nothing more, since the repository was listed.</returns>
  private static RepositoryPermission ToPermission(GitHubRepositoryPermissions? permissions) =>
    permissions switch
    {
      { Admin: true } => RepositoryPermission.Admin,
      { Push: true } => RepositoryPermission.Write,
      var _ => RepositoryPermission.Read,
    };

  #endregion

  #region Implementation of IGitHubUserGateway

  /// <inheritdoc />
  /// <exception cref="UnauthorizedException">GitHub no longer accepts the token.</exception>
  /// <exception cref="BadGatewayException">GitHub refused the reading, does not answer or answered with an unreadable payload.</exception>
  /// <exception cref="GitHubRateLimitedException">GitHub rate limited the request.</exception>
  public async Task<GitHubUser> GetUserAsync(string accessToken, CancellationToken cancellationToken)
  {
    ArgumentException.ThrowIfNullOrWhiteSpace(accessToken);

    var user = await GetAsync(accessToken, "user", GitHubJsonContext.Default.GitHubUserResponse, absentWhenNotFound: false, cancellationToken)
                 .ConfigureAwait(false);

    return new GitHubUser(user!.Id.ToString(CultureInfo.InvariantCulture), user.Login, user.AvatarUrl);
  }

  /// <inheritdoc />
  /// <exception cref="UnauthorizedException">GitHub no longer accepts the token.</exception>
  /// <exception cref="BadGatewayException">
  ///   GitHub refused the reading, does not answer, answered with an unreadable payload, or its pages do not hold the
  ///   installations it counted.
  /// </exception>
  /// <exception cref="GitHubRateLimitedException">GitHub rate limited the request.</exception>
  public async Task<IReadOnlyList<GitHubInstallationInfo>> ListInstallationsAsync(string accessToken, CancellationToken cancellationToken)
  {
    ArgumentException.ThrowIfNullOrWhiteSpace(accessToken);

    var installations = new Dictionary<long, GitHubInstallationInfo>();

    for (var page = 1; page <= ListMaxPages; page++)
    {
      var address = string.Create(CultureInfo.InvariantCulture, $"user/installations?per_page={ListPerPage}&page={page}");
      var read = await GetAsync(accessToken, address, GitHubJsonContext.Default.GitHubUserInstallationsResponse, absentWhenNotFound: false, cancellationToken)
                   .ConfigureAwait(false);

      foreach (var installation in read!.Installations)
      {
        installations[installation.Id] = new GitHubInstallationInfo(
          installation.Id,
          installation.Account.Id.ToString(CultureInfo.InvariantCulture),
          installation.Account.Login ?? string.Empty,
          GitHubAccountTypes.ToAccountType(installation.Account.Type),
          installation.SuspendedAt is not null
        );
      }

      if (read.Installations.Count < ListPerPage)
      {
        // A list that does not hold what GitHub counted would take organizations away from the user: read again later.
        return installations.Count == read.TotalCount
                 ? [.. installations.Values]
                 : throw new BadGatewayException(GitHubErrors.Unavailable, "The installations of the user changed while they were listed.");
      }
    }

    throw new BadGatewayException(GitHubErrors.Unavailable, "The user reaches more installations than this instance reads.");
  }

  /// <inheritdoc />
  /// <exception cref="UnauthorizedException">GitHub no longer accepts the token.</exception>
  /// <exception cref="BadGatewayException">
  ///   GitHub refused the reading, does not answer, answered with an unreadable payload, or its pages do not hold the
  ///   repositories it counted.
  /// </exception>
  /// <exception cref="GitHubRateLimitedException">GitHub rate limited the request.</exception>
  public async Task<IReadOnlyList<GitHubUserRepository>> ListInstallationRepositoriesAsync(
    string accessToken,
    long installationId,
    CancellationToken cancellationToken)
  {
    ArgumentException.ThrowIfNullOrWhiteSpace(accessToken);
    ArgumentOutOfRangeException.ThrowIfNegativeOrZero(installationId);

    var repositories = new Dictionary<long, GitHubUserRepository>();

    for (var page = 1; page <= ListMaxPages; page++)
    {
      var address = string.Create(CultureInfo.InvariantCulture, $"user/installations/{installationId}/repositories?per_page={ListPerPage}&page={page}");
      var read = await GetAsync(accessToken, address, GitHubJsonContext.Default.GitHubUserRepositoriesResponse, absentWhenNotFound: true, cancellationToken)
                   .ConfigureAwait(false);

      // The user cannot reach the installation: it reaches none of its repositories.
      if (read is null)
      {
        return [];
      }

      foreach (var repository in read.Repositories)
      {
        repositories[repository.Id] = new GitHubUserRepository(repository.Id.ToString(CultureInfo.InvariantCulture), ToPermission(repository.Permissions));
      }

      if (read.Repositories.Count < ListPerPage)
      {
        // The pages are numbered: a repository added or removed between two readings shifts them. A list that does
        // not hold what GitHub counted would cut the access of the user short: it is read again later.
        return repositories.Count == read.TotalCount
                 ? [.. repositories.Values]
                 : throw new BadGatewayException(GitHubErrors.Unavailable, "The repositories of the installation changed while they were listed.");
      }
    }

    throw new BadGatewayException(GitHubErrors.Unavailable, "The installation has more repositories than this instance reads.");
  }

  #endregion
}
