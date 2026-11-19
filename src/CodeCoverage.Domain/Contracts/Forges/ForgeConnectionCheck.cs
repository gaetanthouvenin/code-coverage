// ------------------------------------------------------------------------------------------------
// <copyright file="ForgeConnectionCheck.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Diagnostics.CodeAnalysis;

using CodeCoverage.Domain.Guards;

namespace CodeCoverage.Domain.Contracts.Forges;

/// <summary>The result of the verification of a connection by its forge: the account it reaches, or why it is refused.</summary>
/// <remarks>A refusal is an answer of the forge, not a failure: a forge that cannot be reached fails instead.</remarks>
public sealed record ForgeConnectionCheck
{
  #region Ctors

  /// <summary>Initializes a new instance of the <see cref="ForgeConnectionCheck" /> class.</summary>
  /// <param name="account">The account the connection reaches, or <see langword="null" /> for a refusal.</param>
  /// <param name="baseAddress">The address of the API of the forge for the account, or <see langword="null" /> for a refusal.</param>
  /// <param name="refusal">Why the forge refuses the connection, or <see langword="null" /> when it accepts it.</param>
  private ForgeConnectionCheck(ForgeAccountInfo? account, Uri? baseAddress, string? refusal)
  {
    Account = account;
    BaseAddress = baseAddress;
    Refusal = refusal;
    IsRefused = refusal is not null;
  }

  #endregion

  #region Properties

  /// <summary>Gets the account the connection reaches, or <see langword="null" /> when the forge refuses it.</summary>
  public ForgeAccountInfo? Account { get; }

  /// <summary>Gets the absolute address of the API of the forge for the account, or <see langword="null" /> when the forge refuses it.</summary>
  public Uri? BaseAddress { get; }

  /// <summary>Gets why the forge refuses the connection, never a secret, or <see langword="null" /> when it accepts it.</summary>
  public string? Refusal { get; }

  /// <summary>Gets a value indicating whether the forge refuses the connection.</summary>
  [MemberNotNullWhen(false, nameof(Account), nameof(BaseAddress))]
  public bool IsRefused { get; }

  #endregion

  #region Methods

  /// <summary>Builds the result of a connection the forge accepts.</summary>
  /// <param name="account">The account the connection reaches.</param>
  /// <param name="baseAddress">The absolute address of the API of the forge for the account.</param>
  /// <returns>The accepted check.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="account" /> or <paramref name="baseAddress" /> is <see langword="null" />.</exception>
  /// <exception cref="ArgumentException"><paramref name="baseAddress" /> is not an absolute address.</exception>
  public static ForgeConnectionCheck Verified(ForgeAccountInfo account, Uri baseAddress)
  {
    ArgumentNullException.ThrowIfNull(account);
    ArgumentNullException.ThrowIfNull(baseAddress);
    ArgumentException.ThrowIf(!baseAddress.IsAbsoluteUri, "The address of the forge must be absolute.", nameof(baseAddress));

    return new ForgeConnectionCheck(account, baseAddress, null);
  }

  /// <summary>Builds the result of a connection the forge refuses.</summary>
  /// <param name="refusal">Why the forge refuses it, never a secret.</param>
  /// <returns>The refused check.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="refusal" /> is <see langword="null" />.</exception>
  /// <exception cref="ArgumentException"><paramref name="refusal" /> is empty or white space.</exception>
  public static ForgeConnectionCheck Refused(string refusal)
  {
    ArgumentException.ThrowIfNullOrWhiteSpace(refusal);

    return new ForgeConnectionCheck(null, null, refusal);
  }

  #endregion
}
