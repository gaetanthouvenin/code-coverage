// ------------------------------------------------------------------------------------------------
// <copyright file="AzureDevOpsUserGateway.cs" company="Gaëtan Thouvenin">
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
using CodeCoverage.Domain.Contracts.Forges;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Infrastructure.AzureDevOps.Models;
using CodeCoverage.Infrastructure.Forges;

namespace CodeCoverage.Infrastructure.AzureDevOps;

/// <summary>Calls Azure DevOps in the name of a user, with the token Microsoft Entra ID delegated to it.</summary>
/// <param name="client">The typed client of the profile service of Azure DevOps, whose pipeline handles the rate limits and the failures.</param>
/// <remarks>
///   <para>
///     It is the <see cref="IForgeUserGateway" /> of Azure DevOps, keyed by <see cref="Forge.AzureDevOps" />. The profile
///     and the organizations of a user are read on the profile service, outside any organization:
///     <c>_apis/profile/profiles/me</c>, then <c>_apis/accounts?memberId={profile}</c>.
///   </para>
///   <para>
///     A token Azure DevOps refuses is an expired session: it is reported as unauthenticated, and the user signs in
///     again. Azure DevOps answers such a token with a 401, or with a 203 that carries its sign-in page instead of the
///     payload. Every other refusal is a failure of Azure DevOps. The token is never kept nor logged.
///   </para>
///   <para>
///     An account is an Azure DevOps organization the user is a member of. Its connection is the name of the
///     organization, under which the repositories the user reaches are listed with its own token; its identifier is
///     the identifier of the organization, in lower case, that an admitted organization keeps in
///     <c>ExternalAccountId</c>. That this identifier is the <c>instanceId</c> of <c>_apis/connectionData</c> that
///     M7.40 reads is probable, not measured.
///   </para>
///   <para>The permissions of the user on the repositories (M7.54) are not built yet: they are refused with a <see cref="NotSupportedException" />.</para>
/// </remarks>
internal sealed class AzureDevOpsUserGateway(HttpClient client) : IForgeUserGateway
{
  #region Fields

  private const string ForgeName = "Azure DevOps";

  #endregion

  #region Methods

  /// <summary>Reads the profile of the user a token belongs to.</summary>
  /// <param name="accessToken">The user token.</param>
  /// <param name="cancellationToken">A token to cancel the reading.</param>
  /// <returns>The profile.</returns>
  private Task<AzureDevOpsProfile> GetProfileAsync(string accessToken, CancellationToken cancellationToken) =>
    GetAsync(
      accessToken,
      $"_apis/profile/profiles/me?api-version={AzureDevOpsGateway.ApiVersion}",
      AzureDevOpsJsonContext.Default.AzureDevOpsProfile,
      cancellationToken
    );

  /// <summary>Sends a reading with a user token and reads its answer.</summary>
  /// <typeparam name="TModel">The type of the payload.</typeparam>
  /// <param name="accessToken">The user token.</param>
  /// <param name="address">The address, relative to the profile service.</param>
  /// <param name="type">The JSON metadata of the payload.</param>
  /// <param name="cancellationToken">A token to cancel the reading.</param>
  /// <returns>The payload.</returns>
  /// <exception cref="UnauthorizedException">Azure DevOps no longer accepts the token.</exception>
  /// <exception cref="BadGatewayException">Azure DevOps refused the reading or answered with an unreadable payload.</exception>
  private async Task<TModel> GetAsync<TModel>(string accessToken, string address, JsonTypeInfo<TModel> type, CancellationToken cancellationToken)
    where TModel : class
  {
    using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(address, UriKind.Relative));

    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

    using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);

    UnauthorizedException.ThrowIfUserTokenRefused(response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.NonAuthoritativeInformation, ForgeName);

    BadGatewayException.ThrowIfForgeRefused(
      !response.IsSuccessStatusCode,
      string.Create(CultureInfo.InvariantCulture, $"Azure DevOps refused the reading of '{address}': HTTP {(int)response.StatusCode}.")
    );

    try
    {
      return await response.Content.ReadFromJsonAsync(type, cancellationToken).ConfigureAwait(false)
             ?? throw new JsonException("The payload is empty.");
    }
    catch (JsonException exception)
    {
      throw BadGatewayException.ForgeUnavailable($"Azure DevOps answered the reading of '{address}' with an unreadable payload.", exception);
    }
  }

  #endregion

  #region Implementation of IForgeUserGateway

  /// <inheritdoc />
  /// <remarks>The identifier is the identifier of the profile, in lower case; the login is the name Azure DevOps shows. No avatar is given: its address needs a token.</remarks>
  /// <exception cref="UnauthorizedException">Azure DevOps no longer accepts the token.</exception>
  /// <exception cref="BadGatewayException">Azure DevOps refused the reading, does not answer or answered with an unreadable payload.</exception>
  /// <exception cref="ForgeRateLimitedException">Azure DevOps rate limited the request.</exception>
  public async Task<ForgeUser> GetUserAsync(string accessToken, CancellationToken cancellationToken)
  {
    ArgumentException.ThrowIfNullOrWhiteSpace(accessToken);

    var profile = await GetProfileAsync(accessToken, cancellationToken).ConfigureAwait(false);

    return new ForgeUser(profile.Id.ToString("D", CultureInfo.InvariantCulture), profile.DisplayName, null);
  }

  /// <inheritdoc />
  /// <remarks>
  ///   The profile is read first: the organizations are listed by the identifier of its member. Azure DevOps lists them
  ///   in one answer, without pages. No organization is reported suspended: the access of the user is its own token,
  ///   which Azure DevOps accepts or refuses as a whole.
  /// </remarks>
  /// <exception cref="UnauthorizedException">Azure DevOps no longer accepts the token.</exception>
  /// <exception cref="BadGatewayException">Azure DevOps refused the reading, does not answer or answered with an unreadable payload.</exception>
  /// <exception cref="ForgeRateLimitedException">Azure DevOps rate limited the request.</exception>
  public async Task<IReadOnlyList<ForgeUserAccount>> ListAccountsAsync(string accessToken, CancellationToken cancellationToken)
  {
    ArgumentException.ThrowIfNullOrWhiteSpace(accessToken);

    var profile = await GetProfileAsync(accessToken, cancellationToken).ConfigureAwait(false);
    var accounts = await GetAsync(
                     accessToken,
                     string.Create(CultureInfo.InvariantCulture, $"_apis/accounts?memberId={profile.Id:D}&api-version={AzureDevOpsGateway.ApiVersion}"),
                     AzureDevOpsJsonContext.Default.AzureDevOpsListAzureDevOpsAccount,
                     cancellationToken
                   ).ConfigureAwait(false);

    return
    [
      .. accounts.Value
                 .DistinctBy(static account => account.AccountId)
                 .Select(static account => new ForgeUserAccount(
                           account.AccountName,
                           account.AccountId.ToString("D", CultureInfo.InvariantCulture),
                           account.AccountName,
                           OrganizationAccountType.Organization,
                           Suspended: false
                         )
                 ),
    ];
  }

  /// <inheritdoc />
  /// <exception cref="NotSupportedException">The permissions of a user on Azure DevOps are not built yet (M7.54).</exception>
  public Task<IReadOnlyList<ForgeRepositoryPermission>> ListRepositoryPermissionsAsync(
    string accessToken,
    ForgeUserAccount account,
    CancellationToken cancellationToken)
  {
    ArgumentException.ThrowIfNullOrWhiteSpace(accessToken);
    ArgumentNullException.ThrowIfNull(account);

    return Task.FromException<IReadOnlyList<ForgeRepositoryPermission>>(
      new NotSupportedException("The permissions of a user on Azure DevOps are not built yet (M7.54).")
    );
  }

  #endregion
}
