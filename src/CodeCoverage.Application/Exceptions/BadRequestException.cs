// ------------------------------------------------------------------------------------------------
// <copyright file="BadRequestException.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Application.Exceptions;

/// <summary>
///   Exception thrown when a request is inconsistent outside the validation rules.
///   Maps to HTTP 400 Bad Request.
/// </summary>
public sealed class BadRequestException : ApplicationException
{
  #region Ctors

  /// <summary>Initializes a new instance of the <see cref="BadRequestException" /> class.</summary>
  /// <param name="errorCode">The machine-readable error code, in the <c>Feature.Reason</c> format.</param>
  /// <param name="message">The message that describes the error.</param>
  public BadRequestException(string errorCode, string message)
    : base(errorCode, message)
  {
  }

  /// <summary>Initializes a new instance of the <see cref="BadRequestException" /> class with its cause.</summary>
  /// <param name="errorCode">The machine-readable error code, in the <c>Feature.Reason</c> format.</param>
  /// <param name="message">The message that describes the error.</param>
  /// <param name="innerException">The exception that caused the current exception.</param>
  public BadRequestException(string errorCode, string message, Exception innerException)
    : base(errorCode, message, innerException)
  {
  }

  #endregion
}
