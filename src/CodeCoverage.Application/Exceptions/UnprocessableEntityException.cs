// ------------------------------------------------------------------------------------------------
// <copyright file="UnprocessableEntityException.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Application.Exceptions;

/// <summary>
///   Exception thrown when a business rule refuses an otherwise valid request.
///   Maps to HTTP 422 Unprocessable Entity.
/// </summary>
public sealed class UnprocessableEntityException : ApplicationException
{
  #region Ctors

  /// <summary>Initializes a new instance of the <see cref="UnprocessableEntityException" /> class.</summary>
  /// <param name="errorCode">The machine-readable error code, in the <c>Feature.Reason</c> format.</param>
  /// <param name="message">The message that describes the error.</param>
  public UnprocessableEntityException(string errorCode, string message)
    : base(errorCode, message)
  {
  }

  /// <summary>Initializes a new instance of the <see cref="UnprocessableEntityException" /> class with its cause.</summary>
  /// <param name="errorCode">The machine-readable error code, in the <c>Feature.Reason</c> format.</param>
  /// <param name="message">The message that describes the error.</param>
  /// <param name="innerException">The exception that caused the current exception.</param>
  public UnprocessableEntityException(string errorCode, string message, Exception innerException)
    : base(errorCode, message, innerException)
  {
  }

  #endregion
}
