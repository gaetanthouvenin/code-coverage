// ------------------------------------------------------------------------------------------------
// <copyright file="Organization.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Runtime.CompilerServices;

using CodeCoverage.Domain.Operations;

namespace CodeCoverage.Domain.Organizations;

/// <summary>One account of a forge hosted by the instance, personal accounts included.</summary>
/// <remarks>
///   <para>
///     The account is identified by its forge and the identifier the forge gives it, which never
///     changes. A login can be renamed and then claimed by another account of the same forge: when that
///     happens, the row that still holds it gets a tombstone (<see cref="Tombstone" />), which is not a
///     well-formed login and so cannot be resolved.
///   </para>
///   <para>This row holds no business data: it is read before a scope enters an organization.</para>
/// </remarks>
public sealed class Organization
{
  #region Fields

  /// <summary>The maximum length of <see cref="Login" />, whatever the forge.</summary>
  public const int LoginMaxLength = 64;

  /// <summary>The maximum length of <see cref="ExternalAccountId" />; a braced GUID takes 38 characters.</summary>
  public const int ExternalAccountIdMaxLength = 40;

  /// <summary>The start of a tombstone; its tilde cannot appear in a login.</summary>
  public const string TombstonePrefix = "~stale-";

  #endregion

  #region Ctors

  /// <summary>Initializes a new instance of the <see cref="Organization" /> class.</summary>
  /// <param name="forge">The forge of the account.</param>
  /// <param name="externalAccountId">The identifier given by the forge, in lower case.</param>
  /// <param name="login">The login of the account.</param>
  /// <param name="normalizedLogin">The lower-case form of the login.</param>
  /// <param name="accountType">The kind of account.</param>
  /// <param name="state">The place in the lifecycle.</param>
  /// <param name="createdAt">When the row was created, in UTC.</param>
  /// <param name="updatedAt">When the row last changed, in UTC.</param>
  private Organization(
    Forge forge,
    string externalAccountId,
    string login,
    string normalizedLogin,
    OrganizationAccountType accountType,
    OrganizationState state,
    DateTimeOffset createdAt,
    DateTimeOffset updatedAt)
  {
    Forge = forge;
    ExternalAccountId = externalAccountId;
    Login = login;
    NormalizedLogin = normalizedLogin;
    AccountType = accountType;
    State = state;
    CreatedAt = createdAt;
    UpdatedAt = updatedAt;
  }

  #endregion

  #region Properties

  /// <summary>Gets the identifier of the row, assigned when it is saved.</summary>
  public long Id { get; private set; }

  /// <summary>Gets the forge of the account.</summary>
  public Forge Forge { get; private set; }

  /// <summary>Gets the identifier the forge gives the account, in lower case, which never changes.</summary>
  public string ExternalAccountId { get; private set; }

  /// <summary>Gets the login of the account as its forge spells it, or a tombstone.</summary>
  public string Login { get; private set; }

  /// <summary>Gets the lower-case form of <see cref="Login" />, unique on its forge.</summary>
  public string NormalizedLogin { get; private set; }

  /// <summary>Gets the kind of account.</summary>
  public OrganizationAccountType AccountType { get; private set; }

  /// <summary>Gets the place of the organization in its lifecycle.</summary>
  public OrganizationState State { get; private set; }

  /// <summary>Gets why the organization is suspended, or <see langword="null" /> when it is not.</summary>
  public SuspensionReason? SuspensionReason { get; private set; }

  /// <summary>Gets who admitted the organization, or <see langword="null" /> before its admission.</summary>
  public string? AdmittedBy { get; private set; }

  /// <summary>Gets when the row was created, in UTC.</summary>
  public DateTimeOffset CreatedAt { get; private set; }

  /// <summary>Gets when the row last changed, in UTC.</summary>
  public DateTimeOffset UpdatedAt { get; private set; }

  /// <summary>Gets the version of the row, incremented by each change to detect concurrent writes.</summary>
  public int Version { get; private set; }

  #endregion

  #region Methods

  /// <summary>Creates an organization that waits for its admission.</summary>
  /// <param name="forge">The forge of the account.</param>
  /// <param name="externalAccountId">The identifier the forge gives the account.</param>
  /// <param name="login">The login of the account; a reserved login is accepted, <see cref="Admit" /> refuses it.</param>
  /// <param name="accountType">The kind of account.</param>
  /// <param name="at">The current time; converted to UTC.</param>
  /// <returns>The organization, not saved yet.</returns>
  /// <exception cref="ArgumentNullException">
  ///   <paramref name="externalAccountId" /> or <paramref name="login" /> is <see langword="null" />.
  /// </exception>
  /// <exception cref="ArgumentException">
  ///   <paramref name="externalAccountId" /> is empty, white space or too long, or <paramref name="login" />
  ///   is not a well-formed login.
  /// </exception>
  public static Organization CreatePending(
    Forge forge,
    string externalAccountId,
    string login,
    OrganizationAccountType accountType,
    DateTimeOffset at)
  {
    ArgumentException.ThrowIfNullOrWhiteSpace(externalAccountId);

    if (externalAccountId.Length > ExternalAccountIdMaxLength)
    {
      throw new ArgumentException(
        $"The value cannot be longer than {ExternalAccountIdMaxLength} characters.",
        nameof(externalAccountId)
      );
    }

    ThrowIfMalformed(login);

    var utc = at.ToUniversalTime();

    return new Organization(
      forge,
      NormalizeExternalAccountId(externalAccountId),
      login,
      NormalizeLogin(login),
      accountType,
      OrganizationState.Pending,
      utc,
      utc
    );
  }

  /// <summary>Tells whether a text can be the login of an account, whatever its forge.</summary>
  /// <param name="login">The text, possibly <see langword="null" />.</param>
  /// <returns>
  ///   <see langword="true" /> for at most <see cref="LoginMaxLength" /> ASCII letters, digits, hyphens or
  ///   underscores that start with a letter or a digit; otherwise <see langword="false" />. A tombstone is
  ///   never well formed.
  /// </returns>
  /// <remarks>
  ///   One rule serves every forge, so it is wider than each of them: a login that no forge would issue
  ///   simply resolves to nothing. On GitHub, older accounts may hold hyphens that are no longer accepted,
  ///   and Enterprise Managed Users hold an underscore.
  /// </remarks>
  public static bool IsWellFormedLogin(string? login) =>
    !string.IsNullOrEmpty(login)
    && login.Length <= LoginMaxLength
    && char.IsAsciiLetterOrDigit(login[0])
    && login.All(static character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_');

  /// <summary>Gives the form of a login that is compared and stored as unique.</summary>
  /// <param name="login">The login.</param>
  /// <returns>The login in lower case.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="login" /> is <see langword="null" />.</exception>
  public static string NormalizeLogin(string login)
  {
    ArgumentNullException.ThrowIfNull(login);

    return login.ToLowerInvariant();
  }

  /// <summary>Gives the form of an account identifier that is compared and stored as unique.</summary>
  /// <param name="externalAccountId">The identifier given by the forge.</param>
  /// <returns>The identifier in lower case.</returns>
  /// <remarks>
  ///   The unique index then behaves the same under the collations of both database engines, one of which
  ///   ignores the case.
  /// </remarks>
  /// <exception cref="ArgumentNullException"><paramref name="externalAccountId" /> is <see langword="null" />.</exception>
  public static string NormalizeExternalAccountId(string externalAccountId)
  {
    ArgumentNullException.ThrowIfNull(externalAccountId);

    return externalAccountId.ToLowerInvariant();
  }

  /// <summary>Admits a pending organization.</summary>
  /// <param name="admittedBy">Who admits it.</param>
  /// <param name="at">The current time; converted to UTC.</param>
  /// <exception cref="ArgumentNullException"><paramref name="admittedBy" /> is <see langword="null" />.</exception>
  /// <exception cref="ArgumentException"><paramref name="admittedBy" /> is empty, white space or too long.</exception>
  /// <exception cref="InvalidOperationException">
  ///   The organization is not <see cref="OrganizationState.Pending" />, or its login is reserved.
  /// </exception>
  public void Admit(string admittedBy, DateTimeOffset at)
  {
    ArgumentException.ThrowIfNullOrWhiteSpace(admittedBy);

    if (admittedBy.Length > AuditEntry.ActorMaxLength)
    {
      throw new ArgumentException(
        $"The value cannot be longer than {AuditEntry.ActorMaxLength} characters.",
        nameof(admittedBy)
      );
    }

    if (State is not OrganizationState.Pending)
    {
      throw new InvalidOperationException($"Only a pending organization can be admitted; this one is {State}.");
    }

    if (ReservedLogins.IsReserved(Login))
    {
      throw new InvalidOperationException($"The login '{Login}' is reserved and cannot be admitted.");
    }

    State = OrganizationState.Active;
    AdmittedBy = admittedBy;
    Touch(at);
  }

  /// <summary>Suspends the organization; the reason of a first suspension is kept.</summary>
  /// <param name="reason">Why the organization is suspended.</param>
  /// <param name="at">The current time; converted to UTC.</param>
  public void Suspend(SuspensionReason reason, DateTimeOffset at)
  {
    if (State is OrganizationState.Suspended)
    {
      return;
    }

    State = OrganizationState.Suspended;
    SuspensionReason = reason;
    Touch(at);
  }

  /// <summary>Applies the login that the forge reports for the account.</summary>
  /// <param name="newLogin">The login reported by the forge.</param>
  /// <param name="at">The current time; converted to UTC.</param>
  /// <remarks>
  ///   A reserved login suspends an active organization. A pending organization stays pending, since
  ///   <see cref="Admit" /> refuses it as long as the login is reserved.
  /// </remarks>
  /// <exception cref="ArgumentNullException"><paramref name="newLogin" /> is <see langword="null" />.</exception>
  /// <exception cref="ArgumentException"><paramref name="newLogin" /> is not a well-formed login.</exception>
  public void Rename(string newLogin, DateTimeOffset at)
  {
    ThrowIfMalformed(newLogin);

    if (string.Equals(newLogin, Login, StringComparison.Ordinal))
    {
      return;
    }

    Login = newLogin;
    NormalizedLogin = NormalizeLogin(newLogin);
    Touch(at);

    if (State is OrganizationState.Active && ReservedLogins.IsReserved(newLogin))
    {
      State = OrganizationState.Suspended;
      SuspensionReason = Organizations.SuspensionReason.ReservedLogin;
    }
  }

  /// <summary>Applies the kind of account that the forge reports.</summary>
  /// <param name="accountType">The kind of account: a personal account can become an organization.</param>
  /// <param name="at">The current time; converted to UTC.</param>
  public void ChangeAccountType(OrganizationAccountType accountType, DateTimeOffset at)
  {
    if (accountType != AccountType)
    {
      AccountType = accountType;
      Touch(at);
    }
  }

  /// <summary>Gives up the login, now held by another account, and replaces it with a tombstone.</summary>
  /// <param name="at">The current time; converted to UTC.</param>
  /// <remarks>
  ///   The state is unchanged, so that the next synchronization can restore the login of the account.
  ///   Until then the organization cannot be reached by a login.
  /// </remarks>
  public void Tombstone(DateTimeOffset at)
  {
    var tombstone = TombstonePrefix + ExternalAccountId;

    if (string.Equals(tombstone, Login, StringComparison.Ordinal))
    {
      return;
    }

    Login = tombstone;
    NormalizedLogin = NormalizeLogin(tombstone);
    Touch(at);
  }

  /// <summary>Checks that a text can be the login of an account.</summary>
  /// <param name="login">The text.</param>
  /// <param name="name">The name of the parameter, filled by the compiler.</param>
  /// <exception cref="ArgumentNullException"><paramref name="login" /> is <see langword="null" />.</exception>
  /// <exception cref="ArgumentException"><paramref name="login" /> is not a well-formed login.</exception>
  private static void ThrowIfMalformed(string login, [CallerArgumentExpression(nameof(login))] string? name = null)
  {
    ArgumentNullException.ThrowIfNull(login, name);

    if (!IsWellFormedLogin(login))
    {
      throw new ArgumentException($"The login '{login}' is not well formed.", name);
    }
  }

  /// <summary>Records a change of the row.</summary>
  /// <param name="at">The current time; converted to UTC.</param>
  private void Touch(DateTimeOffset at)
  {
    UpdatedAt = at.ToUniversalTime();
    Version++;
  }

  #endregion
}
