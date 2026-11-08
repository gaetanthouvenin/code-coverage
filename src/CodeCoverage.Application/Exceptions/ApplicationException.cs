// ------------------------------------------------------------------------------------------------
// <copyright file="ApplicationException.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Application.Exceptions;

/// <summary>
///   Abstract base class for application-level exceptions.
///   Carries a machine-readable <see cref="ErrorCode" /> used by the global exception handler
///   to produce structured error responses.
///   Maps to HTTP 400 Bad Request unless a derived type says otherwise.
/// </summary>
/// <remarks>
///   The HTTP status code is never carried by the exception, because the Application does not know
///   HTTP: the Api middleware and the Web error service deduce it from the type. Outside this
///   namespace, the name conflicts with <see cref="System.ApplicationException" />, so add the alias
///   <c>using ApplicationException = CodeCoverage.Application.Exceptions.ApplicationException;</c>.
/// </remarks>
public abstract class ApplicationException : Exception
{
  #region Ctors

  /// <summary>
  ///   Initializes a new instance of the <see cref="ApplicationException" /> class
  ///   with the specified error code and message.
  /// </summary>
  /// <param name="errorCode">A machine-readable error code identifying the failure.</param>
  /// <param name="message">A human-readable description of the error.</param>
  /// <exception cref="ArgumentException"><paramref name="errorCode" /> is <see langword="null" />, empty or white space.</exception>
  protected ApplicationException(string errorCode, string message)
    : base(message)
  {
    ArgumentException.ThrowIfNullOrWhiteSpace(errorCode);

    ErrorCode = errorCode;
  }

  /// <summary>
  ///   Initializes a new instance of the <see cref="ApplicationException" /> class
  ///   with the specified error code, message, and inner exception.
  /// </summary>
  /// <param name="errorCode">A machine-readable error code identifying the failure.</param>
  /// <param name="message">A human-readable description of the error.</param>
  /// <param name="innerException">The exception that caused the current exception.</param>
  /// <exception cref="ArgumentException"><paramref name="errorCode" /> is <see langword="null" />, empty or white space.</exception>
  protected ApplicationException(string errorCode, string message, Exception innerException)
    : base(message, innerException)
  {
    ArgumentException.ThrowIfNullOrWhiteSpace(errorCode);

    ErrorCode = errorCode;
  }

  #endregion

  #region Properties

  /// <summary>
  ///   Gets the machine-readable error code identifying the failure, in the <c>Feature.Reason</c>
  ///   format.
  /// </summary>
  public string ErrorCode { get; }

  #endregion
}
