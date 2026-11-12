// ------------------------------------------------------------------------------------------------
// <copyright file="UploadTokenService.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Globalization;

using CodeCoverage.Application.Exceptions;
using CodeCoverage.Application.Features.Repositories;
using CodeCoverage.Application.Features.UploadTokens;
using CodeCoverage.Domain.Operations;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Domain.Repositories;
using CodeCoverage.Domain.Tokens;

namespace CodeCoverage.Application.Services;

/// <summary>What the six messages of the upload tokens share, one per use case and per audience.</summary>
/// <param name="tokens">The upload tokens.</param>
/// <param name="repositories">The tracked repositories.</param>
/// <param name="generator">The generator of the values.</param>
/// <param name="hasher">The hasher of the values.</param>
/// <param name="currentUser">The principal of the scope.</param>
/// <param name="clock">The clock.</param>
/// <param name="auditLog">The audit trail.</param>
internal sealed class UploadTokenService(
  IUploadTokenRepository tokens,
  IRepositoryRepository repositories,
  ITokenGenerator generator,
  ITokenHasher hasher,
  ICurrentUser currentUser,
  IClock clock,
  IAuditLog auditLog)
{
  #region Fields

  /// <summary>The audit action of a created token.</summary>
  internal const string CreateAuditAction = "UploadTokens.Create";

  /// <summary>The audit action of a revoked token.</summary>
  internal const string RevokeAuditAction = "UploadTokens.Revoke";

  /// <summary>The separator of the encoded form of a slug, which accepts every form of owner.</summary>
  private const string SlugSeparator = "::::";

  /// <summary>How many characters of a generated token are kept to recognize it.</summary>
  private const int GeneratedHintLength = 8;

  /// <summary>How many of the last characters of an imported token are kept to recognize it.</summary>
  private const int ImportedHintLength = 4;

  #endregion

  #region Properties

  /// <summary>Gets the name of the principal, as the audit trail writes it.</summary>
  private string ActorName => currentUser.GetActorName();

  #endregion

  #region Methods

  /// <summary>Creates a token from a generated or an imported value.</summary>
  /// <param name="scope">What the token gives access to.</param>
  /// <param name="repositoryId">The repository of a token of that scope.</param>
  /// <param name="label">The name given to the token.</param>
  /// <param name="importedValue">The imported value, or <see langword="null" /> to generate one.</param>
  /// <param name="expiresAt">When it expires, or <see langword="null" />.</param>
  /// <param name="cancellationToken">A token to cancel the saving.</param>
  /// <returns>The token, with its value in clear.</returns>
  /// <exception cref="ConflictException">The same value is already a token of this organization (<see cref="UploadTokenErrors.AlreadyImported" />).</exception>
  public async Task<IssuedToken> CreateAsync(
    UploadTokenScope scope,
    long? repositoryId,
    string label,
    string? importedValue,
    DateTimeOffset? expiresAt,
    CancellationToken cancellationToken)
  {
    var value = importedValue ?? generator.NewUploadToken();
    var hint = importedValue is null ? value[..GeneratedHintLength] : value[^ImportedHintLength..];
    var token = UploadToken.Create(scope, repositoryId, label, hint, hasher.Hash(value), importedValue is not null, ActorName, clock.UtcNow, expiresAt);

    var added = await tokens.TryAddAsync(token, cancellationToken).ConfigureAwait(false);

    ConflictException.ThrowIfUploadTokenAlreadyImported(added);

    await RecordAsync(CreateAuditAction, token, cancellationToken).ConfigureAwait(false);

    return new IssuedToken(token, value);
  }

  /// <summary>Revokes a token of the given owner; revoking it again changes nothing.</summary>
  /// <param name="id">The identifier of the token.</param>
  /// <param name="scope">The scope it must have.</param>
  /// <param name="repositoryId">The repository it must have, for that scope.</param>
  /// <param name="cancellationToken">A token to cancel the saving.</param>
  /// <returns>A task that completes when it is saved.</returns>
  /// <exception cref="NotFoundException">The token is not one of that owner (<see cref="UploadTokenErrors.NotFound" />).</exception>
  public async Task RevokeAsync(long id, UploadTokenScope scope, long? repositoryId, CancellationToken cancellationToken)
  {
    var token = await tokens.FindAsync(id, cancellationToken).ConfigureAwait(false);

    NotFoundException.ThrowIfUploadTokenNotFound(token, scope, repositoryId);

    if (token.RevokedAt is not null)
    {
      return;
    }

    token.Revoke(clock.UtcNow);
    await tokens.UpdateAsync(token, cancellationToken).ConfigureAwait(false);
    await RecordAsync(RevokeAuditAction, token, cancellationToken).ConfigureAwait(false);
  }

  /// <summary>Finds a repository of the organization of the scope.</summary>
  /// <param name="owner">The owner, as received from the caller.</param>
  /// <param name="name">The name of the repository.</param>
  /// <param name="cancellationToken">A token to cancel the reading.</param>
  /// <returns>The identifier of the repository.</returns>
  /// <exception cref="NotFoundException">The repository does not exist (<see cref="RepositoryErrors.NotFound" />).</exception>
  public async Task<long> GetRepositoryIdAsync(string owner, string name, CancellationToken cancellationToken)
  {
    var repository = RepositorySlug.TryParse(string.Concat(owner, SlugSeparator, name), out var slug)
                       ? await repositories.FindBySlugAsync(slug, cancellationToken).ConfigureAwait(false)
                       : null;

    NotFoundException.ThrowIfRepositoryNotFound(repository);

    return repository.Id;
  }

  /// <summary>Records a change of a token, without its value.</summary>
  /// <param name="action">The audit action.</param>
  /// <param name="token">The token.</param>
  /// <param name="cancellationToken">A token to cancel the saving.</param>
  /// <returns>A task that completes when it is saved.</returns>
  private Task RecordAsync(string action, UploadToken token, CancellationToken cancellationToken)
  {
    var details = new Dictionary<string, string>(StringComparer.Ordinal)
    {
      ["Scope"] = token.Scope.ToString(),
      ["Label"] = token.Label,
      ["Imported"] = token.Imported ? "true" : "false",
    };

    return auditLog.RecordAsync(action, token.Id.ToString(CultureInfo.InvariantCulture), details, cancellationToken);
  }

  #endregion
}
