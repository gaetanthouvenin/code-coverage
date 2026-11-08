// ------------------------------------------------------------------------------------------------
// <copyright file="ExceptionExtensions.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace CodeCoverage.Domain.Guards;

/// <summary>
///   Adds to the exceptions of the framework the guards it does not provide, in the form of its own
///   <see cref="ArgumentNullException.ThrowIfNull(object?, string?)" />.
/// </summary>
/// <remarks>
///   A message is evaluated before the guard runs, even when nothing is thrown: it must not read what
///   the condition is checking.
/// </remarks>
public static class ExceptionExtensions
{
  #region Methods

  extension(ArgumentException)
  {
    /// <summary>Throws an <see cref="ArgumentException" /> when a condition holds.</summary>
    /// <param name="condition">The condition of the refusal.</param>
    /// <param name="message">The message of the exception.</param>
    /// <param name="paramName">The name of the refused parameter.</param>
    /// <exception cref="ArgumentNullException"><paramref name="message" /> is <see langword="null" />.</exception>
    /// <exception cref="ArgumentException"><paramref name="condition" /> is <see langword="true" />.</exception>
    public static void ThrowIf([DoesNotReturnIf(true)] bool condition, string message, string? paramName)
    {
      ArgumentNullException.ThrowIfNull(message);

      if (condition)
      {
        throw new ArgumentException(message, paramName);
      }
    }
  }

  extension(ArgumentOutOfRangeException)
  {
    /// <summary>
    ///   Throws an <see cref="ArgumentOutOfRangeException" /> when a value is not a member of its
    ///   enumeration.
    /// </summary>
    /// <typeparam name="TEnum">The enumeration.</typeparam>
    /// <param name="value">The value to check.</param>
    /// <param name="paramName">The name of the parameter, deduced from <paramref name="value" />.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    ///   <paramref name="value" /> is not defined by <typeparamref name="TEnum" />.
    /// </exception>
    public static void ThrowIfUndefined<TEnum>(
      TEnum value,
      [CallerArgumentExpression(nameof(value))] string? paramName = null)
      where TEnum : struct, Enum
    {
      if (!Enum.IsDefined(value))
      {
        throw new ArgumentOutOfRangeException(paramName, value, $"The value is not defined by {typeof(TEnum).Name}.");
      }
    }
  }

  extension(InvalidOperationException)
  {
    /// <summary>Throws an <see cref="InvalidOperationException" /> when a condition holds.</summary>
    /// <param name="condition">The condition of the refusal.</param>
    /// <param name="message">The message of the exception.</param>
    /// <exception cref="ArgumentNullException"><paramref name="message" /> is <see langword="null" />.</exception>
    /// <exception cref="InvalidOperationException"><paramref name="condition" /> is <see langword="true" />.</exception>
    public static void ThrowIf([DoesNotReturnIf(true)] bool condition, string message)
    {
      ArgumentNullException.ThrowIfNull(message);

      if (condition)
      {
        throw new InvalidOperationException(message);
      }
    }
  }

  #endregion
}
