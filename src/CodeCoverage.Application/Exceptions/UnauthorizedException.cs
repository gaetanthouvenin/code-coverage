// ------------------------------------------------------------------------------------------------
// <copyright file="UnauthorizedException.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Application.Exceptions;

/// <summary>
///   Exception thrown when no user is signed in, or when the upload or API token is missing or invalid.
///   Maps to HTTP 401 Unauthorized.
/// </summary>
public sealed class UnauthorizedException : ApplicationException
{
  #region Ctors

  /// <summary>Initializes a new instance of the <see cref="UnauthorizedException" /> class.</summary>
  /// <param name="errorCode">The machine-readable error code, in the <c>Feature.Reason</c> format.</param>
  /// <param name="message">The message that describes the error.</param>
  public UnauthorizedException(string errorCode, string message)
    : base(errorCode, message)
  {
  }

  /// <summary>Initializes a new instance of the <see cref="UnauthorizedException" /> class with its cause.</summary>
  /// <param name="errorCode">The machine-readable error code, in the <c>Feature.Reason</c> format.</param>
  /// <param name="message">The message that describes the error.</param>
  /// <param name="innerException">The exception that caused the current exception.</param>
  public UnauthorizedException(string errorCode, string message, Exception innerException)
    : base(errorCode, message, innerException)
  {
  }

  #endregion
}
