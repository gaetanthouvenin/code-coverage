// ------------------------------------------------------------------------------------------------
// <copyright file="AuditEntry.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Runtime.CompilerServices;

namespace CodeCoverage.Domain.Operations;

/// <summary>One line of the audit trail: who did what, on what, and when.</summary>
/// <remarks>
///   <para>
///     An entry is immutable once created. Its <see cref="Details" /> never hold a secret: a key that
///     contains <c>Secret</c>, whatever its case, is refused by <see cref="Create" />.
///   </para>
///   <para>
///     The actor is the GitHub login of a user, <see cref="SystemActor" /> for the jobs and the
///     webhooks, or <see cref="AnonymousActor" />. Both markers hold brackets, which a GitHub login
///     cannot contain: they never collide with a real user.
///   </para>
/// </remarks>
public sealed class AuditEntry
{
  #region Fields

  /// <summary>The actor of the actions done by the application itself (jobs, webhooks).</summary>
  public const string SystemActor = "[system]";

  /// <summary>The actor of the actions done while nobody is signed in.</summary>
  public const string AnonymousActor = "[anonymous]";

  /// <summary>The maximum length of <see cref="Actor" />.</summary>
  public const int ActorMaxLength = 64;

  /// <summary>The maximum length of <see cref="Action" />.</summary>
  public const int ActionMaxLength = 128;

  /// <summary>The maximum length of <see cref="Target" />.</summary>
  public const int TargetMaxLength = 256;

  /// <summary>The fragment that marks a detail key as secret.</summary>
  private const string SecretMarker = "Secret";

  #endregion

  #region Ctors

  /// <summary>Initializes a new instance of the <see cref="AuditEntry" /> class.</summary>
  /// <param name="at">When the action was done, in UTC.</param>
  /// <param name="actor">Who did the action.</param>
  /// <param name="action">What was done.</param>
  /// <param name="target">What the action applies to.</param>
  /// <param name="details">The context of the action.</param>
  private AuditEntry(
    DateTimeOffset at,
    string actor,
    string action,
    string target,
    IReadOnlyDictionary<string, string> details)
  {
    At = at;
    Actor = actor;
    Action = action;
    Target = target;
    Details = details;
  }

  #endregion

  #region Properties

  /// <summary>Gets the identifier of the entry, assigned when it is saved.</summary>
  public long Id { get; private set; }

  /// <summary>Gets when the action was done, in UTC.</summary>
  public DateTimeOffset At { get; private set; }

  /// <summary>
  ///   Gets who did the action: a GitHub login, <see cref="SystemActor" /> or
  ///   <see cref="AnonymousActor" />.
  /// </summary>
  public string Actor { get; private set; }

  /// <summary>Gets what was done, as <c>Feature.Verb</c> (for example <c>Settings.Update</c>).</summary>
  public string Action { get; private set; }

  /// <summary>Gets what the action applies to (for example the key of a setting).</summary>
  public string Target { get; private set; }

  /// <summary>Gets the context of the action, by key; empty when there is none.</summary>
  public IReadOnlyDictionary<string, string> Details { get; private set; }

  #endregion

  #region Methods

  /// <summary>Creates an entry.</summary>
  /// <param name="at">When the action was done; converted to UTC.</param>
  /// <param name="actor">Who did the action.</param>
  /// <param name="action">What was done.</param>
  /// <param name="target">What the action applies to.</param>
  /// <param name="details">The context of the action, or <see langword="null" /> for none.</param>
  /// <returns>The entry, not saved yet.</returns>
  /// <exception cref="ArgumentNullException">
  ///   <paramref name="actor" />, <paramref name="action" /> or <paramref name="target" /> is
  ///   <see langword="null" />, or <paramref name="details" /> holds a <see langword="null" /> value.
  /// </exception>
  /// <exception cref="ArgumentException">
  ///   <paramref name="actor" />, <paramref name="action" /> or <paramref name="target" /> is empty,
  ///   white space or longer than its maximum length, or <paramref name="details" /> holds an empty key
  ///   or a key that contains <c>Secret</c>.
  /// </exception>
  public static AuditEntry Create(
    DateTimeOffset at,
    string actor,
    string action,
    string target,
    IReadOnlyDictionary<string, string>? details)
  {
    ThrowIfInvalid(actor, ActorMaxLength);
    ThrowIfInvalid(action, ActionMaxLength);
    ThrowIfInvalid(target, TargetMaxLength);

    return new AuditEntry(at.ToUniversalTime(), actor, action, target, CopyDetails(details));
  }

  /// <summary>Copies the details, refusing a secret key and a missing value.</summary>
  /// <param name="details">The details, or <see langword="null" />.</param>
  /// <returns>A copy of the details, ordered by key; empty for <see langword="null" />.</returns>
  /// <exception cref="ArgumentNullException">A value is <see langword="null" />.</exception>
  /// <exception cref="ArgumentException">A key is empty, white space or contains <c>Secret</c>.</exception>
  private static SortedDictionary<string, string> CopyDetails(IReadOnlyDictionary<string, string>? details)
  {
    var copy = new SortedDictionary<string, string>(StringComparer.Ordinal);

    if (details is null)
    {
      return copy;
    }

    foreach (var (key, value) in details)
    {
      ArgumentException.ThrowIfNullOrWhiteSpace(key, nameof(details));

      if (key.Contains(SecretMarker, StringComparison.OrdinalIgnoreCase))
      {
        throw new ArgumentException(
          $"The audit details cannot hold a secret: the key '{key}' is refused.",
          nameof(details)
        );
      }

      ArgumentNullException.ThrowIfNull(value, nameof(details));
      copy.Add(key, value);
    }

    return copy;
  }

  /// <summary>Checks a mandatory text against its maximum length.</summary>
  /// <param name="value">The text.</param>
  /// <param name="maxLength">The maximum length.</param>
  /// <param name="name">The name of the parameter, filled by the compiler.</param>
  /// <exception cref="ArgumentNullException"><paramref name="value" /> is <see langword="null" />.</exception>
  /// <exception cref="ArgumentException"><paramref name="value" /> is empty, white space or too long.</exception>
  private static void ThrowIfInvalid(
    string value,
    int maxLength,
    [CallerArgumentExpression(nameof(value))] string? name = null)
  {
    ArgumentException.ThrowIfNullOrWhiteSpace(value, name);

    if (value.Length > maxLength)
    {
      throw new ArgumentException($"The value cannot be longer than {maxLength} characters.", name);
    }
  }

  #endregion
}
