// ------------------------------------------------------------------------------------------------
// <copyright file="ValidationException.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using FluentValidation.Results;

using CodeCoverage.Application.Exceptions.ErrorCodes;

namespace CodeCoverage.Application.Exceptions;

/// <summary>
///   Exception thrown when one or more FluentValidation rules fail.
///   Contains the error messages and the error codes grouped by property name.
///   Maps to HTTP 400 Bad Request.
/// </summary>
public sealed class ValidationException : ApplicationException
{
  #region Ctors

  /// <summary>
  ///   Initializes a new instance of the <see cref="ValidationException" /> class
  ///   from a collection of FluentValidation failures.
  /// </summary>
  /// <param name="failures">The validation failures to include in the exception.</param>
  /// <exception cref="ArgumentNullException"><paramref name="failures" /> is <see langword="null" />.</exception>
  /// <remarks>The error code is <see cref="GeneralErrors.ValidationFailed" />.</remarks>
  public ValidationException(IEnumerable<ValidationFailure> failures)
    : base(GeneralErrors.ValidationFailed, "One or more validation errors occurred.")
  {
    ArgumentNullException.ThrowIfNull(failures);

    var groups = failures.GroupBy(f => f.PropertyName, StringComparer.Ordinal).ToArray();

    Errors = groups.ToDictionary(g => g.Key, g => g.Select(f => f.ErrorMessage).ToArray(), StringComparer.Ordinal)
                   .AsReadOnly();
    ErrorCodes = groups.ToDictionary(g => g.Key, g => g.Select(f => f.ErrorCode ?? string.Empty).ToArray(), StringComparer.Ordinal)
                       .AsReadOnly();
  }

  #endregion

  #region Properties

  /// <summary>Gets the validation error messages grouped by property name.</summary>
  public IReadOnlyDictionary<string, string[]> Errors { get; }

  /// <summary>Gets the error codes of the failures, grouped by property name.</summary>
  /// <remarks>
  ///   Each array is parallel to the one of <see cref="Errors" /> for the same property: the code at an index is the
  ///   one of the message at that index, e.g. <c>Setting.InvalidValue</c>, or an empty string when the rule gave none.
  ///   The interface translates the code, and falls back on the message when the code has no translation.
  /// </remarks>
  public IReadOnlyDictionary<string, string[]> ErrorCodes { get; }

  #endregion
}
