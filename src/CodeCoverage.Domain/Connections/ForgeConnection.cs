// ------------------------------------------------------------------------------------------------
// <copyright file="ForgeConnection.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Guards;
using CodeCoverage.Domain.Organizations;

namespace CodeCoverage.Domain.Connections;

/// <summary>
///   The connection of an organization to a forge other than GitHub, which holds the protected credentials the
///   instance uses to reach it (ADR-017).
/// </summary>
/// <remarks>
///   <para>
///     The row is global, like an installation of the GitHub App: a webhook reads it to find its organization, before
///     the scope has entered one. GitHub never gets a connection: its installations stay in <c>Installations</c>.
///   </para>
///   <para>
///     The secrets are protected by <c>ISecretProtector</c> before they reach the connection, like the token of a
///     Codecov import: the plain values are never kept, and nothing here can tell them apart from protected ones.
///   </para>
/// </remarks>
public sealed class ForgeConnection
{
  #region Fields

  /// <summary>The maximum length of <see cref="Id" />.</summary>
  public const int IdMaxLength = 64;

  /// <summary>The maximum length of <see cref="BaseAddress" />, in characters.</summary>
  public const int BaseAddressMaxLength = 512;

  /// <summary>The maximum length of <see cref="ServiceAccountExternalId" />; a braced GUID takes 38 characters.</summary>
  public const int ServiceAccountExternalIdMaxLength = 64;

  /// <summary>The maximum length of <see cref="LastError" />; a longer error is cut.</summary>
  public const int LastErrorMaxLength = 1000;

  #endregion

  #region Ctors

  /// <summary>Initializes a new instance of the <see cref="ForgeConnection" /> class.</summary>
  /// <param name="id">The identifier the instance gives the connection.</param>
  /// <param name="externalAccountId">The identifier the forge gives the account of the organization, in lower case.</param>
  /// <param name="baseAddress">The address of the API of the forge for this account.</param>
  /// <param name="credentialProtected">The protected credential.</param>
  /// <param name="webhookSecretProtected">The protected secret of the webhooks.</param>
  private ForgeConnection(string id, string externalAccountId, Uri baseAddress, string credentialProtected, string webhookSecretProtected)
  {
    Id = id;
    ExternalAccountId = externalAccountId;
    BaseAddress = baseAddress;
    CredentialProtected = credentialProtected;
    WebhookSecretProtected = webhookSecretProtected;
  }

  #endregion

  #region Properties

  /// <summary>Gets the identifier the instance gives the connection, never the forge; it appears in the address of its webhooks.</summary>
  public string Id { get; private set; }

  /// <summary>Gets the identifier of the organization of the connection.</summary>
  public long OrganizationId { get; private set; }

  /// <summary>Gets the forge of the connection, never <see cref="Organizations.Forge.GitHub" />.</summary>
  public Forge Forge { get; private set; }

  /// <summary>Gets the kind of credential the connection holds.</summary>
  public ForgeConnectionKind Kind { get; private set; }

  /// <summary>Gets the identifier the forge gives the account of the organization, in lower case.</summary>
  public string ExternalAccountId { get; private set; }

  /// <summary>Gets the address of the API of the forge for this account.</summary>
  public Uri BaseAddress { get; private set; }

  /// <summary>Gets the directory tenant that issues the credential, or <see langword="null" /> when the forge has none.</summary>
  public Guid? TenantId { get; private set; }

  /// <summary>
  ///   Gets the identifier the forge gives the account the credential acts as, which authors the comments, or
  ///   <see langword="null" /> when it is not known yet.
  /// </summary>
  public string? ServiceAccountExternalId { get; private set; }

  /// <summary>Gets the credential, protected by <c>ISecretProtector</c>.</summary>
  public string CredentialProtected { get; private set; }

  /// <summary>Gets the current secret of the webhooks, protected by <c>ISecretProtector</c>.</summary>
  public string WebhookSecretProtected { get; private set; }

  /// <summary>
  ///   Gets the secret of the webhooks before the last rotation, protected by <c>ISecretProtector</c>, or
  ///   <see langword="null" /> before a first rotation; a delivery signed by it is still accepted.
  /// </summary>
  public string? WebhookSecretPreviousProtected { get; private set; }

  /// <summary>Gets when the credential expires, in UTC, or <see langword="null" /> when it does not or is not known.</summary>
  public DateTimeOffset? ExpiresAt { get; private set; }

  /// <summary>Gets the health of the connection.</summary>
  public ForgeConnectionStatus Status { get; private set; }

  /// <summary>Gets when the connection was made, in UTC.</summary>
  public DateTimeOffset ConnectedAt { get; private set; }

  /// <summary>Gets when the connection was suspended, in UTC, or <see langword="null" /> when it is not.</summary>
  public DateTimeOffset? SuspendedAt { get; private set; }

  /// <summary>Gets when the connection was last checked against its forge, in UTC, or <see langword="null" /> before a first check.</summary>
  public DateTimeOffset? LastCheckedAt { get; private set; }

  /// <summary>Gets why the last check failed, or <see langword="null" /> when it succeeded or none ran.</summary>
  public string? LastError { get; private set; }

  #endregion

  #region Methods

  /// <summary>Tells whether a text can be the identifier of a connection.</summary>
  /// <param name="id">The text, possibly <see langword="null" />.</param>
  /// <returns>
  ///   <see langword="true" /> for at most <see cref="IdMaxLength" /> lower-case ASCII letters, digits or hyphens that
  ///   start with a letter or a digit; otherwise <see langword="false" />. Such an identifier is safe in an address and
  ///   compares the same way under the collations of both database engines.
  /// </returns>
  public static bool IsWellFormedId(string? id) =>
    !string.IsNullOrEmpty(id)
    && id.Length <= IdMaxLength
    && (char.IsAsciiLetterLower(id[0]) || char.IsAsciiDigit(id[0]))
    && id.All(static character => char.IsAsciiLetterLower(character) || char.IsAsciiDigit(character) || character is '-');

  /// <summary>Records a new connection, active from now.</summary>
  /// <param name="id">The identifier the instance gives the connection.</param>
  /// <param name="organizationId">The organization of the connection.</param>
  /// <param name="forge">The forge of the connection; GitHub is refused.</param>
  /// <param name="kind">The kind of credential.</param>
  /// <param name="externalAccountId">The identifier the forge gives the account of the organization.</param>
  /// <param name="baseAddress">The absolute address of the API of the forge for this account.</param>
  /// <param name="tenantId">The directory tenant that issues the credential, or <see langword="null" />.</param>
  /// <param name="serviceAccountExternalId">The identifier of the account the credential acts as, or <see langword="null" />.</param>
  /// <param name="credentialProtected">The credential protected by <c>ISecretProtector</c>, never the plain value.</param>
  /// <param name="webhookSecretProtected">The secret of the webhooks protected by <c>ISecretProtector</c>, never the plain value.</param>
  /// <param name="expiresAt">When the credential expires, or <see langword="null" />; converted to UTC.</param>
  /// <param name="at">The current time; converted to UTC.</param>
  /// <returns>The connection, not saved yet.</returns>
  /// <exception cref="ArgumentNullException">
  ///   <paramref name="id" />, <paramref name="externalAccountId" />, <paramref name="baseAddress" />,
  ///   <paramref name="credentialProtected" /> or <paramref name="webhookSecretProtected" /> is <see langword="null" />.
  /// </exception>
  /// <exception cref="ArgumentOutOfRangeException">
  ///   <paramref name="organizationId" /> is not positive, or <paramref name="forge" /> or <paramref name="kind" /> is
  ///   not defined.
  /// </exception>
  /// <exception cref="ArgumentException">
  ///   <paramref name="id" /> is not well formed, <paramref name="forge" /> is GitHub, a text is empty, white space or too
  ///   long, or <paramref name="baseAddress" /> is not an absolute address.
  /// </exception>
  public static ForgeConnection Create(
    string id,
    long organizationId,
    Forge forge,
    ForgeConnectionKind kind,
    string externalAccountId,
    Uri baseAddress,
    Guid? tenantId,
    string? serviceAccountExternalId,
    string credentialProtected,
    string webhookSecretProtected,
    DateTimeOffset? expiresAt,
    DateTimeOffset at)
  {
    ArgumentNullException.ThrowIfNull(id);
    ArgumentException.ThrowIf(!IsWellFormedId(id), $"The connection identifier '{id}' is not well formed.", nameof(id));
    ArgumentOutOfRangeException.ThrowIfNegativeOrZero(organizationId);
    ArgumentOutOfRangeException.ThrowIfUndefined(forge);
    ArgumentException.ThrowIf(forge is Forge.GitHub, "GitHub keeps its installations and gets no forge connection.", nameof(forge));
    ArgumentOutOfRangeException.ThrowIfUndefined(kind);
    ArgumentException.ThrowIfNullOrWhiteSpace(externalAccountId);
    ThrowIfTooLong(externalAccountId, Organization.ExternalAccountIdMaxLength, nameof(externalAccountId));
    ArgumentNullException.ThrowIfNull(baseAddress);
    ArgumentException.ThrowIf(!baseAddress.IsAbsoluteUri, "The address of the forge must be absolute.", nameof(baseAddress));
    ThrowIfTooLong(baseAddress.OriginalString, BaseAddressMaxLength, nameof(baseAddress));

    if (serviceAccountExternalId is not null)
    {
      ArgumentException.ThrowIfNullOrWhiteSpace(serviceAccountExternalId);
      ThrowIfTooLong(serviceAccountExternalId, ServiceAccountExternalIdMaxLength, nameof(serviceAccountExternalId));
    }

    ArgumentException.ThrowIfNullOrWhiteSpace(credentialProtected);
    ArgumentException.ThrowIfNullOrWhiteSpace(webhookSecretProtected);

    return new ForgeConnection(id, Organization.NormalizeExternalAccountId(externalAccountId), baseAddress, credentialProtected, webhookSecretProtected)
    {
      OrganizationId = organizationId,
      Forge = forge,
      Kind = kind,
      TenantId = tenantId,
      ServiceAccountExternalId = serviceAccountExternalId,
      ExpiresAt = expiresAt?.ToUniversalTime(),
      Status = ForgeConnectionStatus.Active,
      ConnectedAt = at.ToUniversalTime(),
    };
  }

  /// <summary>Suspends the connection: nothing reaches the forge through it any more.</summary>
  /// <param name="at">The current time; converted to UTC.</param>
  /// <remarks>The date of a first suspension is kept when the connection is suspended again.</remarks>
  public void Suspend(DateTimeOffset at)
  {
    if (Status is ForgeConnectionStatus.Suspended)
    {
      return;
    }

    Status = ForgeConnectionStatus.Suspended;
    SuspendedAt = at.ToUniversalTime();
  }

  /// <summary>Replaces the credential of the connection, once its forge has verified the new one.</summary>
  /// <param name="credentialProtected">The new credential protected by <c>ISecretProtector</c>, never the plain value.</param>
  /// <param name="serviceAccountExternalId">
  ///   The identifier of the account the new credential acts as, or <see langword="null" /> to keep the one known.
  /// </param>
  /// <param name="expiresAt">When the new credential expires, or <see langword="null" /> when it does not or is not known; converted to UTC.</param>
  /// <remarks>
  ///   The previous credential is forgotten: only the secret of the webhooks keeps a previous value. The forge has just
  ///   accepted the new credential, so the error of the last check is cleared and a
  ///   <see cref="ForgeConnectionStatus.Failing" /> or <see cref="ForgeConnectionStatus.Expiring" /> connection becomes
  ///   <see cref="ForgeConnectionStatus.Active" /> until its next check; a rotation never lifts a suspension.
  /// </remarks>
  /// <exception cref="ArgumentNullException"><paramref name="credentialProtected" /> is <see langword="null" />.</exception>
  /// <exception cref="ArgumentException">
  ///   <paramref name="credentialProtected" /> is empty or white space, or <paramref name="serviceAccountExternalId" /> is
  ///   empty, white space or too long.
  /// </exception>
  public void RotateCredential(string credentialProtected, string? serviceAccountExternalId, DateTimeOffset? expiresAt)
  {
    ArgumentException.ThrowIfNullOrWhiteSpace(credentialProtected);

    if (serviceAccountExternalId is not null)
    {
      ArgumentException.ThrowIfNullOrWhiteSpace(serviceAccountExternalId);
      ThrowIfTooLong(serviceAccountExternalId, ServiceAccountExternalIdMaxLength, nameof(serviceAccountExternalId));
      ServiceAccountExternalId = serviceAccountExternalId;
    }

    CredentialProtected = credentialProtected;
    ExpiresAt = expiresAt?.ToUniversalTime();
    LastError = null;

    if (Status is ForgeConnectionStatus.Failing or ForgeConnectionStatus.Expiring)
    {
      Status = ForgeConnectionStatus.Active;
    }
  }

  /// <summary>Replaces the secret of the webhooks, and keeps the current one as the previous one.</summary>
  /// <param name="webhookSecretProtected">The new secret protected by <c>ISecretProtector</c>, never the plain value.</param>
  /// <remarks>
  ///   A delivery the forge signed before it learnt the new secret is still accepted against the previous one; the
  ///   secret before that is forgotten.
  /// </remarks>
  /// <exception cref="ArgumentNullException"><paramref name="webhookSecretProtected" /> is <see langword="null" />.</exception>
  /// <exception cref="ArgumentException"><paramref name="webhookSecretProtected" /> is empty or white space.</exception>
  public void RotateWebhookSecret(string webhookSecretProtected)
  {
    ArgumentException.ThrowIfNullOrWhiteSpace(webhookSecretProtected);

    WebhookSecretPreviousProtected = WebhookSecretProtected;
    WebhookSecretProtected = webhookSecretProtected;
  }

  /// <summary>Records the result of a check of the connection against its forge.</summary>
  /// <param name="at">The time of the check; converted to UTC.</param>
  /// <param name="error">Why the check failed, or <see langword="null" /> when it succeeded; cut to <see cref="LastErrorMaxLength" />.</param>
  /// <param name="expiryWarning">How long before <see cref="ExpiresAt" /> a successful check marks the connection expiring.</param>
  /// <remarks>
  ///   A failed check marks the connection <see cref="ForgeConnectionStatus.Failing" />, a successful one
  ///   <see cref="ForgeConnectionStatus.Expiring" /> within <paramref name="expiryWarning" /> of its expiry, otherwise
  ///   <see cref="ForgeConnectionStatus.Active" />. A check never suspends a connection, nor lifts its suspension.
  /// </remarks>
  /// <exception cref="ArgumentOutOfRangeException"><paramref name="expiryWarning" /> is negative.</exception>
  public void RecordCheck(DateTimeOffset at, string? error, TimeSpan expiryWarning)
  {
    ArgumentOutOfRangeException.ThrowIfLessThan(expiryWarning, TimeSpan.Zero);

    var utc = at.ToUniversalTime();

    LastCheckedAt = utc;
    LastError = string.IsNullOrWhiteSpace(error) ? null : error[..Math.Min(error.Length, LastErrorMaxLength)];

    if (Status is ForgeConnectionStatus.Suspended)
    {
      return;
    }

    if (LastError is not null)
    {
      Status = ForgeConnectionStatus.Failing;
    }
    else if (ExpiresAt is { } expiresAt && expiresAt - utc <= expiryWarning)
    {
      Status = ForgeConnectionStatus.Expiring;
    }
    else
    {
      Status = ForgeConnectionStatus.Active;
    }
  }

  /// <summary>Checks that a text is not longer than its column.</summary>
  /// <param name="value">The text.</param>
  /// <param name="maxLength">The maximum length.</param>
  /// <param name="name">The name of the parameter.</param>
  /// <exception cref="ArgumentException"><paramref name="value" /> is longer than <paramref name="maxLength" />.</exception>
  private static void ThrowIfTooLong(string value, int maxLength, string name) =>
    ArgumentException.ThrowIf(value.Length > maxLength, $"The value cannot be longer than {maxLength} characters.", name);

  #endregion
}
