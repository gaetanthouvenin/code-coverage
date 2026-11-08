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
///   Contains the error messages grouped by property name.
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

    Errors = failures.GroupBy(f => f.PropertyName, StringComparer.Ordinal)
                     .ToDictionary(g => g.Key, g => g.Select(f => f.ErrorMessage).ToArray(), StringComparer.Ordinal)
                     .AsReadOnly();
  }

  #endregion

  #region Properties

  /// <summary>Gets the validation error messages grouped by property name.</summary>
  public IReadOnlyDictionary<string, string[]> Errors { get; }

  #endregion
}
