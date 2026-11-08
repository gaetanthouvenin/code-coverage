// ------------------------------------------------------------------------------------------------
// <copyright file="ForbiddenException.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Application.Exceptions;

/// <summary>
///   Exception thrown when the current principal lacks the permission required by the request.
///   Maps to HTTP 403 Forbidden.
/// </summary>
public sealed class ForbiddenException : ApplicationException
{
  #region Ctors

  /// <summary>Initializes a new instance of the <see cref="ForbiddenException" /> class.</summary>
  /// <param name="errorCode">The machine-readable error code, in the <c>Feature.Reason</c> format.</param>
  /// <param name="message">The message that describes the error.</param>
  public ForbiddenException(string errorCode, string message)
    : base(errorCode, message)
  {
  }

  /// <summary>Initializes a new instance of the <see cref="ForbiddenException" /> class with its cause.</summary>
  /// <param name="errorCode">The machine-readable error code, in the <c>Feature.Reason</c> format.</param>
  /// <param name="message">The message that describes the error.</param>
  /// <param name="innerException">The exception that caused the current exception.</param>
  public ForbiddenException(string errorCode, string message, Exception innerException)
    : base(errorCode, message, innerException)
  {
  }

  #endregion
}
