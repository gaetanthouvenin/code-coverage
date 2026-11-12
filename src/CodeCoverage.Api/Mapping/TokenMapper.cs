// ------------------------------------------------------------------------------------------------
// <copyright file="TokenMapper.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using CodeCoverage.Api.Contracts.Tokens;
using CodeCoverage.Application.Exceptions;
using CodeCoverage.Application.Features.Access;
using CodeCoverage.Application.Features.Access.Commands.CreateApiToken;
using CodeCoverage.Application.Features.Access.Commands.RevokeApiToken;
using CodeCoverage.Application.Features.Access.Queries.ListApiTokens;
using CodeCoverage.Application.Features.Organizations;
using CodeCoverage.Application.Features.Repositories;
using CodeCoverage.Application.Features.UploadTokens.Commands.CreateOrganizationUploadToken;
using CodeCoverage.Application.Features.UploadTokens.Commands.CreateRepositoryUploadToken;
using CodeCoverage.Application.Features.UploadTokens.Commands.RevokeOrganizationUploadToken;
using CodeCoverage.Application.Features.UploadTokens.Commands.RevokeRepositoryUploadToken;
using CodeCoverage.Application.Features.UploadTokens.Queries.ListOrganizationUploadTokens;
using CodeCoverage.Application.Features.UploadTokens.Queries.ListRepositoryUploadTokens;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Tokens;

namespace CodeCoverage.Api.Mapping;

/// <summary>Converts the upload tokens and the personal API tokens between the Domain and the API.</summary>
/// <remarks>A value in clear leaves only in an issued response, once; a list never carries a value or a hash.</remarks>
internal static class TokenMapper
{
  #region Methods

  /// <summary>Creates an upload token of an organization and converts it for the API.</summary>
  /// <param name="sender">The request sender.</param>
  /// <param name="forge">The forge segment of the address.</param>
  /// <param name="organization">The login of the organization.</param>
  /// <param name="request">The request.</param>
  /// <param name="cancellationToken">A token to cancel the request.</param>
  /// <returns>The token, with its value in clear.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="sender" /> or <paramref name="request" /> is <see langword="null" />.</exception>
  public static async Task<IssuedUploadTokenResponse> CreateOrganizationUploadTokenResponseAsync(
    this ISender sender,
    string forge,
    string organization,
    CreateUploadTokenRequest request,
    CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(sender);
    ArgumentNullException.ThrowIfNull(request);

    var command = new CreateOrganizationUploadTokenCommand(ToOrganizationForge(forge), organization, request.Label, request.ImportedValue, request.ExpiresAt);

    return ToResponse(await sender.Send(command, cancellationToken).ConfigureAwait(false));
  }

  /// <summary>Creates an upload token of a repository and converts it for the API.</summary>
  /// <param name="sender">The request sender.</param>
  /// <param name="forge">The forge segment of the address.</param>
  /// <param name="owner">The owner segment of the address.</param>
  /// <param name="repo">The name of the repository.</param>
  /// <param name="request">The request.</param>
  /// <param name="cancellationToken">A token to cancel the request.</param>
  /// <returns>The token, with its value in clear.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="sender" /> or <paramref name="request" /> is <see langword="null" />.</exception>
  public static async Task<IssuedUploadTokenResponse> CreateRepositoryUploadTokenResponseAsync(
    this ISender sender,
    string forge,
    string owner,
    string repo,
    CreateUploadTokenRequest request,
    CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(sender);
    ArgumentNullException.ThrowIfNull(request);

    var command = new CreateRepositoryUploadTokenCommand(ToRepositoryForge(forge), owner, repo, request.Label, request.ImportedValue, request.ExpiresAt);

    return ToResponse(await sender.Send(command, cancellationToken).ConfigureAwait(false));
  }

  /// <summary>Lists the upload tokens of an organization and converts them for the API.</summary>
  /// <param name="sender">The request sender.</param>
  /// <param name="forge">The forge segment of the address.</param>
  /// <param name="organization">The login of the organization.</param>
  /// <param name="cancellationToken">A token to cancel the request.</param>
  /// <returns>The tokens, without their values.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="sender" /> is <see langword="null" />.</exception>
  public static async Task<IReadOnlyList<UploadTokenResponse>> ListOrganizationUploadTokensResponseAsync(
    this ISender sender,
    string forge,
    string organization,
    CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(sender);

    var tokens = await sender.Send(new ListOrganizationUploadTokensQuery(ToOrganizationForge(forge), organization), cancellationToken).ConfigureAwait(false);

    return [.. tokens.Select(ToResponse)];
  }

  /// <summary>Lists the upload tokens of a repository and converts them for the API.</summary>
  /// <param name="sender">The request sender.</param>
  /// <param name="forge">The forge segment of the address.</param>
  /// <param name="owner">The owner segment of the address.</param>
  /// <param name="repo">The name of the repository.</param>
  /// <param name="cancellationToken">A token to cancel the request.</param>
  /// <returns>The tokens, without their values.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="sender" /> is <see langword="null" />.</exception>
  public static async Task<IReadOnlyList<UploadTokenResponse>> ListRepositoryUploadTokensResponseAsync(
    this ISender sender,
    string forge,
    string owner,
    string repo,
    CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(sender);

    var tokens = await sender.Send(new ListRepositoryUploadTokensQuery(ToRepositoryForge(forge), owner, repo), cancellationToken).ConfigureAwait(false);

    return [.. tokens.Select(ToResponse)];
  }

  /// <summary>Revokes an upload token of an organization.</summary>
  /// <param name="sender">The request sender.</param>
  /// <param name="forge">The forge segment of the address.</param>
  /// <param name="organization">The login of the organization.</param>
  /// <param name="id">The identifier of the token.</param>
  /// <param name="cancellationToken">A token to cancel the request.</param>
  /// <returns>A task that completes when the token is revoked.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="sender" /> is <see langword="null" />.</exception>
  public static async Task RevokeOrganizationUploadTokenAsync(this ISender sender, string forge, string organization, long id, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(sender);

    await sender.Send(new RevokeOrganizationUploadTokenCommand(ToOrganizationForge(forge), organization, id), cancellationToken).ConfigureAwait(false);
  }

  /// <summary>Revokes an upload token of a repository.</summary>
  /// <param name="sender">The request sender.</param>
  /// <param name="forge">The forge segment of the address.</param>
  /// <param name="owner">The owner segment of the address.</param>
  /// <param name="repo">The name of the repository.</param>
  /// <param name="id">The identifier of the token.</param>
  /// <param name="cancellationToken">A token to cancel the request.</param>
  /// <returns>A task that completes when the token is revoked.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="sender" /> is <see langword="null" />.</exception>
  public static async Task RevokeRepositoryUploadTokenAsync(this ISender sender, string forge, string owner, string repo, long id, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(sender);

    await sender.Send(new RevokeRepositoryUploadTokenCommand(ToRepositoryForge(forge), owner, repo, id), cancellationToken).ConfigureAwait(false);
  }

  /// <summary>Creates a personal API token and converts it for the API.</summary>
  /// <param name="sender">The request sender.</param>
  /// <param name="request">The request.</param>
  /// <param name="cancellationToken">A token to cancel the request.</param>
  /// <returns>The token, with its value in clear.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="sender" /> or <paramref name="request" /> is <see langword="null" />.</exception>
  /// <exception cref="BadRequestException">The forge segment names no forge.</exception>
  public static async Task<IssuedApiTokenResponse> CreateApiTokenResponseAsync(this ISender sender, CreateApiTokenRequest request, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(sender);
    ArgumentNullException.ThrowIfNull(request);

    Forge? forge = null;

    if (request.Forge is { } segment)
    {
      forge = ForgeSegments.TryParse(segment, out var parsed)
                ? parsed
                : throw new BadRequestException(AccessErrors.InvalidApiTokenOrganization, "The forge of the organization is not known.");
    }

    var issued = await sender.Send(new CreateApiTokenCommand(request.Name, forge, request.Organization, request.ExpiresAt), cancellationToken).ConfigureAwait(false);
    var token = issued.Token;

    return new IssuedApiTokenResponse(token.Id, issued.Value, token.Hint, token.Name, token.IsInstanceToken, token.ExpiresAt);
  }

  /// <summary>Lists the personal API tokens of the user and converts them for the API.</summary>
  /// <param name="sender">The request sender.</param>
  /// <param name="cancellationToken">A token to cancel the request.</param>
  /// <returns>The tokens, without their values.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="sender" /> is <see langword="null" />.</exception>
  public static async Task<IReadOnlyList<ApiTokenResponse>> ListApiTokensResponseAsync(this ISender sender, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(sender);

    var tokens = await sender.Send(new ListApiTokensQuery(), cancellationToken).ConfigureAwait(false);

    return [.. tokens.Select(static t => new ApiTokenResponse(t.Id, t.Name, t.Hint, t.IsInstanceToken, t.CreatedAt, t.ExpiresAt, t.LastUsedAt, t.RevokedAt))];
  }

  /// <summary>Revokes a personal API token of the user.</summary>
  /// <param name="sender">The request sender.</param>
  /// <param name="id">The identifier of the token.</param>
  /// <param name="cancellationToken">A token to cancel the request.</param>
  /// <returns>A task that completes when the token is revoked.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="sender" /> is <see langword="null" />.</exception>
  public static async Task RevokeApiTokenAsync(this ISender sender, long id, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(sender);

    await sender.Send(new RevokeApiTokenCommand(id), cancellationToken).ConfigureAwait(false);
  }

  /// <summary>Reads the forge segment of the address of an organization: an unknown forge is an unknown organization.</summary>
  /// <param name="forge">The segment.</param>
  /// <returns>The forge.</returns>
  /// <exception cref="NotFoundException">The segment names no forge.</exception>
  private static Forge ToOrganizationForge(string forge) =>
    ForgeSegments.TryParse(forge, out var parsed)
      ? parsed
      : throw new NotFoundException(OrganizationErrors.NotFound, "The organization does not exist or cannot be read.");

  /// <summary>Reads the forge segment of the address of a repository: an unknown forge is an unknown repository.</summary>
  /// <param name="forge">The segment.</param>
  /// <returns>The forge.</returns>
  /// <exception cref="NotFoundException">The segment names no forge.</exception>
  private static Forge ToRepositoryForge(string forge) =>
    ForgeSegments.TryParse(forge, out var parsed)
      ? parsed
      : throw new NotFoundException(RepositoryErrors.NotFound, "The repository does not exist or cannot be read.");

  private static IssuedUploadTokenResponse ToResponse(IssuedToken issued) =>
    new(issued.Token.Id, issued.Value, issued.Token.Hint, issued.Token.Label, issued.Token.ExpiresAt);

  private static UploadTokenResponse ToResponse(UploadToken token) =>
    new(
      token.Id,
      token.Scope.ToString(),
      token.Label,
      token.Hint,
      token.Imported,
      token.CreatedBy,
      token.CreatedAt,
      token.ExpiresAt,
      token.LastUsedAt,
      token.RevokedAt,
      token.RotationAdvisedAt
    );

  #endregion
}
