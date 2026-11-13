// ------------------------------------------------------------------------------------------------
// <copyright file="UiResult.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Diagnostics.CodeAnalysis;

namespace CodeCoverage.Web.Messaging;

/// <summary>The outcome of a request sent by a page through <see cref="UiRequestRunner" />.</summary>
/// <typeparam name="T">The type of the value the request returns.</typeparam>
/// <remarks>
///   <para>A result is in exactly one of three states:</para>
///   <list type="bullet">
///     <item><description><see cref="Succeeded" />: the request returned <see cref="Value" />;</description></item>
///     <item>
///       <description>
///         <see cref="NotFound" />: the requested resource does not exist or is not readable by the
///         user; the page shows its « not found » empty state;
///       </description>
///     </item>
///     <item>
///       <description>
///         neither: the request was refused, and the runner already told the user (a toast, or the
///         messages of the form); the page has nothing more to do.
///       </description>
///     </item>
///   </list>
/// </remarks>
public sealed class UiResult<T>
{
  #region Ctors

  /// <summary>Initializes a new instance of the <see cref="UiResult{T}" /> class.</summary>
  /// <param name="succeeded">Whether the request returned a value.</param>
  /// <param name="value">The value returned, or <see langword="default" />.</param>
  /// <param name="notFound">Whether the requested resource was not found.</param>
  /// <param name="forbidden">Whether the server refused the user.</param>
  /// <param name="unauthenticated">Whether the server did not recognize the caller.</param>
  private UiResult(bool succeeded, T? value, bool notFound, bool forbidden = false, bool unauthenticated = false)
  {
    Succeeded = succeeded;
    Value = value;
    NotFound = notFound;
    Forbidden = forbidden;
    Unauthenticated = unauthenticated;
  }

  #endregion

  #region Properties

  /// <summary>Gets a value indicating whether the request returned a value.</summary>
  [MemberNotNullWhen(true, nameof(Value))]
  public bool Succeeded { get; }

  /// <summary>
  ///   Gets the value returned by the request, or <see langword="default" /> when it did not
  ///   succeed.
  /// </summary>
  public T? Value { get; }

  /// <summary>
  ///   Gets a value indicating whether the requested resource does not exist or is not readable by
  ///   the user, the two being indistinguishable.
  /// </summary>
  public bool NotFound { get; }

  /// <summary>
  ///   Gets a value indicating whether the server refused the user: the page shows its denied state. The navigation
  ///   only hides entries, the server stays the authority.
  /// </summary>
  public bool Forbidden { get; }

  /// <summary>
  ///   Gets a value indicating whether the server did not recognize the caller, for a page that says so itself: the
  ///   first-start wizard, whose own redirection would send a visitor back to it forever.
  /// </summary>
  public bool Unauthenticated { get; }

  #endregion

  #region Methods

  /// <summary>Creates the result of a request that returned a value.</summary>
  /// <param name="value">The value returned.</param>
  /// <returns>A result that <see cref="Succeeded" />.</returns>
  internal static UiResult<T> Success(T value) => new(true, value, false);

  /// <summary>Creates the result of a request whose resource was not found.</summary>
  /// <returns>A <see cref="NotFound" /> result.</returns>
  internal static UiResult<T> Missing() => new(false, default, true);

  /// <summary>Creates the result of a request refused and already reported to the user.</summary>
  /// <returns>A result neither <see cref="Succeeded" /> nor <see cref="NotFound" />.</returns>
  internal static UiResult<T> Refused() => new(false, default, false);

  /// <summary>Creates the result of a request the server refused to the user, reported by the page itself.</summary>
  /// <returns>A <see cref="Forbidden" /> result.</returns>
  internal static UiResult<T> Denied() => new(false, default, false, true);

  /// <summary>Creates the result of a request whose caller the server did not recognize, reported by the page itself.</summary>
  /// <returns>An <see cref="Unauthenticated" /> result.</returns>
  internal static UiResult<T> Unknown() => new(false, default, false, false, true);

  #endregion
}
