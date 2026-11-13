// ------------------------------------------------------------------------------------------------
// <copyright file="TokenMapper.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using CodeCoverage.Application.Features.Repositories.Commands.ImportBadgeToken;
using CodeCoverage.Application.Features.Repositories.Commands.RotateBadgeToken;
using CodeCoverage.Application.Features.Repositories.Queries.GetBadgeToken;
using CodeCoverage.Application.Features.UploadTokens.Commands.CreateRepositoryUploadToken;
using CodeCoverage.Application.Features.UploadTokens.Commands.RevokeRepositoryUploadToken;
using CodeCoverage.Application.Features.UploadTokens.Queries.ListRepositoryUploadTokens;
using CodeCoverage.Domain.Tokens;
using CodeCoverage.Web.ViewModels.Tokens;

namespace CodeCoverage.Web.Mapping;

/// <summary>Reads and writes the tokens for the pages: no page names a Domain type, and no value is kept anywhere.</summary>
internal static class TokenMapper
{
  #region Methods

  /// <summary>Reads the upload tokens of a repository, the revoked ones included.</summary>
  /// <param name="sender">The request sender.</param>
  /// <param name="forge">The forge segment of the address.</param>
  /// <param name="owner">The owner segment of the address.</param>
  /// <param name="name">The name of the repository.</param>
  /// <param name="cancellationToken">A token to cancel the reading.</param>
  /// <returns>The tokens, the newest first; none when the forge segment is not one the console serves.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="sender" /> is <see langword="null" />.</exception>
  public static async Task<IReadOnlyList<UploadTokenViewModel>> ListRepositoryUploadTokenViewModelsAsync(
    this ISender sender,
    string forge,
    string owner,
    string name,
    CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(sender);

    if (!RepositoryMapper.TryParseForge(forge, out var parsed))
    {
      return [];
    }

    var tokens = await sender.Send(new ListRepositoryUploadTokensQuery(parsed, owner, name), cancellationToken).ConfigureAwait(false);

    return [.. tokens.OrderByDescending(static t => t.CreatedAt).Select(ToViewModel)];
  }

  /// <summary>Creates an upload token of a repository, whose value is given back this one time.</summary>
  /// <param name="sender">The request sender.</param>
  /// <param name="forge">The forge segment of the address.</param>
  /// <param name="owner">The owner segment of the address.</param>
  /// <param name="name">The name of the repository.</param>
  /// <param name="label">The name given to the token.</param>
  /// <param name="cancellationToken">A token to cancel the request.</param>
  /// <returns>The token with its value, or <see langword="null" /> when the forge segment is not one the console serves.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="sender" /> is <see langword="null" />.</exception>
  public static async Task<IssuedTokenViewModel?> CreateRepositoryUploadTokenAsync(
    this ISender sender,
    string forge,
    string owner,
    string name,
    string label,
    CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(sender);

    if (!RepositoryMapper.TryParseForge(forge, out var parsed))
    {
      return null;
    }

    var issued = await sender.Send(new CreateRepositoryUploadTokenCommand(parsed, owner, name, label, null, null), cancellationToken).ConfigureAwait(false);

    return new IssuedTokenViewModel(issued.Token.Label, issued.Value);
  }

  /// <summary>Imports the <c>CODECOV_TOKEN</c> of a repository: the pipelines keep working without any change.</summary>
  /// <param name="sender">The request sender.</param>
  /// <param name="forge">The forge segment of the address.</param>
  /// <param name="owner">The owner segment of the address.</param>
  /// <param name="name">The name of the repository.</param>
  /// <param name="label">The name given to the token.</param>
  /// <param name="value">The value held by the user.</param>
  /// <param name="cancellationToken">A token to cancel the request.</param>
  /// <returns><see langword="true" /> once the token is saved.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="sender" /> is <see langword="null" />.</exception>
  public static async Task<bool> ImportRepositoryUploadTokenAsync(
    this ISender sender,
    string forge,
    string owner,
    string name,
    string label,
    string value,
    CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(sender);

    if (!RepositoryMapper.TryParseForge(forge, out var parsed))
    {
      return false;
    }

    await sender.Send(new CreateRepositoryUploadTokenCommand(parsed, owner, name, label, value, null), cancellationToken).ConfigureAwait(false);

    return true;
  }

  /// <summary>Revokes an upload token of a repository: anything still using it stops working.</summary>
  /// <param name="sender">The request sender.</param>
  /// <param name="forge">The forge segment of the address.</param>
  /// <param name="owner">The owner segment of the address.</param>
  /// <param name="name">The name of the repository.</param>
  /// <param name="tokenId">The identifier of the token.</param>
  /// <param name="cancellationToken">A token to cancel the request.</param>
  /// <returns><see langword="true" /> once the token is revoked.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="sender" /> is <see langword="null" />.</exception>
  public static async Task<bool> RevokeRepositoryUploadTokenAsync(
    this ISender sender,
    string forge,
    string owner,
    string name,
    long tokenId,
    CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(sender);

    if (!RepositoryMapper.TryParseForge(forge, out var parsed))
    {
      return false;
    }

    await sender.Send(new RevokeRepositoryUploadTokenCommand(parsed, owner, name, tokenId), cancellationToken).ConfigureAwait(false);

    return true;
  }

  /// <summary>Reads the badge token of a repository, the one its README snippet carries.</summary>
  /// <param name="sender">The request sender.</param>
  /// <param name="forge">The forge segment of the address.</param>
  /// <param name="owner">The owner segment of the address.</param>
  /// <param name="name">The name of the repository.</param>
  /// <param name="cancellationToken">A token to cancel the reading.</param>
  /// <returns>The token, or <see langword="null" /> for a public repository or a forge the console does not serve.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="sender" /> is <see langword="null" />.</exception>
  public static async Task<string?> GetBadgeTokenAsync(
    this ISender sender,
    string forge,
    string owner,
    string name,
    CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(sender);

    if (!RepositoryMapper.TryParseForge(forge, out var parsed))
    {
      return null;
    }

    return await sender.Send(new GetBadgeTokenQuery(parsed, owner, name), cancellationToken).ConfigureAwait(false);
  }

  /// <summary>Replaces the badge token of a repository: the snippets of the README must be updated.</summary>
  /// <param name="sender">The request sender.</param>
  /// <param name="forge">The forge segment of the address.</param>
  /// <param name="owner">The owner segment of the address.</param>
  /// <param name="name">The name of the repository.</param>
  /// <param name="cancellationToken">A token to cancel the request.</param>
  /// <returns><see langword="true" /> once the token is replaced.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="sender" /> is <see langword="null" />.</exception>
  public static async Task<bool> RotateBadgeTokenAsync(
    this ISender sender,
    string forge,
    string owner,
    string name,
    CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(sender);

    if (!RepositoryMapper.TryParseForge(forge, out var parsed))
    {
      return false;
    }

    // The new value is read again by the panel, like any other: it is not carried through the page.
    await sender.Send(new RotateBadgeTokenCommand(parsed, owner, name), cancellationToken).ConfigureAwait(false);

    return true;
  }

  /// <summary>Imports the graphing token of Codecov: the badges of the READMEs keep working.</summary>
  /// <param name="sender">The request sender.</param>
  /// <param name="forge">The forge segment of the address.</param>
  /// <param name="owner">The owner segment of the address.</param>
  /// <param name="name">The name of the repository.</param>
  /// <param name="value">The value held by the user.</param>
  /// <param name="cancellationToken">A token to cancel the request.</param>
  /// <returns><see langword="true" /> once the token is saved.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="sender" /> is <see langword="null" />.</exception>
  public static async Task<bool> ImportBadgeTokenAsync(
    this ISender sender,
    string forge,
    string owner,
    string name,
    string value,
    CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(sender);

    if (!RepositoryMapper.TryParseForge(forge, out var parsed))
    {
      return false;
    }

    await sender.Send(new ImportBadgeTokenCommand(parsed, owner, name, value), cancellationToken).ConfigureAwait(false);

    return true;
  }

  // The hint is shown as it is stored: the start of a generated value, the end of an imported one.
  private static UploadTokenViewModel ToViewModel(UploadToken token) =>
    new(token.Id, token.Label, token.Hint, token.Imported, token.CreatedAt, token.LastUsedAt, token.RevokedAt);

  #endregion
}
